using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Newtonsoft.Json;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Assets.Shaders;
using ReiEditor.Models.Services.Assets.Sync;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Render;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Controls.Assets;
using ReiEditor.ViewModels.Utils;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

public class MaterialMonitorDrawerViewModel : BaseMonitorDrawer
{
    public string AssetName { get; }
    public string AssetId { get; }
    public string AssetIdLabel { get; }
    public AssetPickerViewModel ShaderPicker { get; }
    public ObservableCollection<BaseViewModel> ShaderProperties { get; } = new();

    #region StatusText

    private string _statusText = "Loading material...";
    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    #endregion

    #region IsMaterialLoaded

    private bool _isMaterialLoaded;
    public bool IsMaterialLoaded
    {
        get => _isMaterialLoaded;
        private set => SetField(ref _isMaterialLoaded, value);
    }

    #endregion

    #region HasShaderProperties

    private bool _hasShaderProperties;
    public bool HasShaderProperties
    {
        get => _hasShaderProperties;
        private set => SetField(ref _hasShaderProperties, value);
    }

    #endregion

    #region ShaderPropertiesStatusText

    private string _shaderPropertiesStatusText = "";
    public string ShaderPropertiesStatusText
    {
        get => _shaderPropertiesStatusText;
        private set => SetField(ref _shaderPropertiesStatusText, value);
    }

    #endregion

    #region UseDepth

    private bool _useDepth = true;
    public bool UseDepth
    {
        get => _useDepth;
        set
        {
            if (!SetField(ref _useDepth, value)) return;
            _material?.SetUseDepth(value);
            SyncRuntimeMaterial();
        }
    }

    #endregion

    #region SortingOrder

    private int _sortingOrder = 1000;
    public int SortingOrder
    {
        get => _sortingOrder;
        set
        {
            if (!SetField(ref _sortingOrder, value)) return;
            _material?.SetSortingOrder(value);
            SyncRuntimeMaterial();
        }
    }

    #endregion

    private Material? _material;
    private readonly IAssetsService _assetsService;
    private readonly IShaderRegistry _shaderRegistry;
    private readonly IAssetSearchService _assetSearchService;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IAssetTypeMapper _assetTypeMapper;
    private readonly IAssetRuntimeSyncService _assetRuntimeSyncService;
    private readonly IProjectAssetFocusService _projectAssetFocusService;
    private readonly List<(SerializedProperty Property, Action<object?> Handler)> _propertySubscriptions = new();
    private readonly List<(ShaderUniformInfo Uniform, SerializedProperty RootProperty)> _uniformProperties = new();
    private CancellationTokenSource? _runtimeSyncDebounceCTS;
    private DispatcherTimer? _runtimePullTimer;
    private bool _suppressRuntimeSync;
    private string _lastRuntimeJson = "";
    private const int RuntimeSyncDebounceDelayMs = 40;
    private const int RuntimePullIntervalMs = 200;

#pragma warning disable CS8618
    public MaterialMonitorDrawerViewModel() { }
#pragma warning restore CS8618

    public MaterialMonitorDrawerViewModel(
        IAssetSelectable assetSelection,
        IAssetsService assetsService,
        IAssetSearchService assetSearchService,
        IShaderRegistry shaderRegistry,
        IAssetRegistry assetRegistry,
        IAssetTypeMapper assetTypeMapper,
        IAssetRuntimeSyncService assetRuntimeSyncService,
        IProjectAssetFocusService projectAssetFocusService)
    {
        AssetName = assetSelection.AssetName;
        AssetId = assetSelection.AssetId;
        AssetIdLabel = string.IsNullOrWhiteSpace(AssetId) ? "ID: <missing>" : $"ID: {AssetId}";
        _assetsService = assetsService;
        _assetSearchService = assetSearchService;
        _shaderRegistry = shaderRegistry;
        _assetRegistry = assetRegistry;
        _assetTypeMapper = assetTypeMapper;
        _assetRuntimeSyncService = assetRuntimeSyncService;
        _projectAssetFocusService = projectAssetFocusService;

        ShaderPicker = new AssetPickerViewModel(
            assetRegistry,
            shaderRegistry.BuildEntries(),
            HandleShaderChanged);
        ShaderPicker.RefreshSearchResultsForAll();

        _ = LoadMaterialState();
        StartRuntimePullLoop();
    }

    public override void Dispose()
    {
        base.Dispose();
        PersistUniformValuesFromEditors();
        CancelRuntimeSyncDebounce();
        CancelRuntimePullLoop();
        SyncRuntimeMaterialImmediate();
        UnsubscribeFromUniformPropertyChanges();
        ShaderProperties.ClearAndDispose();
        ShaderPicker.Dispose();
    }

    private async Task LoadMaterialState()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(AssetId))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusText = "Material id is missing.";
                    IsMaterialLoaded = false;
                });
                return;
            }

            _material = await _assetsService.Load<Material>(AssetId);
            if (_material == null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusText = "Failed to load material asset.";
                    IsMaterialLoaded = false;
                });
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _suppressRuntimeSync = true;
                ShaderPicker.SyncSelectedAsset(_material.ShaderAssetId);
                UseDepth = _material.UseDepth;
                SortingOrder = _material.SortingOrder;
                RebuildShaderProperties(_material.ShaderAssetId, new Dictionary<string, object?>(_material.Properties));
                StatusText = "";
                IsMaterialLoaded = true;
                _suppressRuntimeSync = false;
            });
        }
        catch (Exception e)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = $"Failed to load material asset. {e.Message}";
                IsMaterialLoaded = false;
            });
        }
    }

    private void HandleShaderChanged(string? shaderAssetId, string? _fullPath)
    {
        if (_material == null) return;

        var targetShaderAssetId = shaderAssetId ?? "";
        if (string.Equals(_material.ShaderAssetId, targetShaderAssetId, StringComparison.Ordinal)) return;

        PersistUniformValuesFromEditors();
        var existingValues = new Dictionary<string, object?>(_material.Properties);
        _material.SetShaderAssetId(targetShaderAssetId);
        RebuildShaderProperties(targetShaderAssetId, existingValues);
        SyncRuntimeMaterial();
    }

    private void RebuildShaderProperties(string shaderAssetId, IReadOnlyDictionary<string, object?> existingValues)
    {
        UnsubscribeFromUniformPropertyChanges();
        ShaderProperties.ClearAndDispose();
        _uniformProperties.Clear();

        if (_material == null)
        {
            HasShaderProperties = false;
            ShaderPropertiesStatusText = "";
            return;
        }

        if (string.IsNullOrWhiteSpace(shaderAssetId))
        {
            _material.Properties.Clear();
            HasShaderProperties = false;
            ShaderPropertiesStatusText = "Material shader is not set.";
            return;
        }

        if (!_shaderRegistry.TryGetById(shaderAssetId, out var shader))
        {
            _material.Properties.Clear();
            HasShaderProperties = false;
            ShaderPropertiesStatusText = "Selected shader asset was not found.";
            return;
        }

        var nextValues = new Dictionary<string, object?>();

        foreach (var uniform in shader.Uniforms.Where(x => x.IsSupported))
        {
            existingValues.TryGetValue(uniform.Name, out var rawValue);
            try
            {
                var rootProperty = MaterialShaderPropertyUtils.CreateSerializedProperty(uniform, rawValue);
                var viewModel = CreatePropertyViewModel(uniform, rootProperty);
                if (viewModel == null)
                {
                    continue;
                }

                ShaderProperties.Add(viewModel);
                _uniformProperties.Add((uniform, rootProperty));
                SubscribeToUniformPropertyChanges(uniform, rootProperty);
                nextValues[uniform.Name] = MaterialShaderPropertyUtils.ConvertSerializedPropertyToMaterialValue(uniform.Type, rootProperty);
            }
            catch
            {
                if (rawValue != null)
                {
                    nextValues[uniform.Name] = rawValue;
                }
            }
        }

        _material.Properties.Clear();
        foreach (var (name, value) in nextValues)
        {
            _material.Properties[name] = value;
        }

        HasShaderProperties = ShaderProperties.Count > 0;
        ShaderPropertiesStatusText = HasShaderProperties
            ? ""
            : "Selected shader has no supported editable uniforms.";
    }

    private BaseViewModel? CreatePropertyViewModel(ShaderUniformInfo uniform, SerializedProperty property)
    {
        return uniform.Type switch
        {
            ShaderUniformType.Float => new FloatPropertyViewModel(property),
            ShaderUniformType.Integer => new IntegerPropertyViewModel(property),
            ShaderUniformType.Color => new ColorPropertyViewModel(property),
            ShaderUniformType.Texture => new AssetPropertyViewModel(property, _assetSearchService, _assetRegistry, _assetTypeMapper, _projectAssetFocusService),
            _ => null
        };
    }

    private void SubscribeToUniformPropertyChanges(ShaderUniformInfo uniform, SerializedProperty rootProperty)
    {
        foreach (var observedProperty in MaterialShaderPropertyUtils.GetObservedProperties(uniform.Type, rootProperty))
        {
            Action<object?> handler = _ => ApplyUniformPropertyValue(uniform, rootProperty);
            observedProperty.ValueChangedEvent += handler;
            _propertySubscriptions.Add((observedProperty, handler));
        }
    }

    private void UnsubscribeFromUniformPropertyChanges()
    {
        foreach (var (property, handler) in _propertySubscriptions)
        {
            property.ValueChangedEvent -= handler;
        }
        _propertySubscriptions.Clear();
    }

    private void ApplyUniformPropertyValue(ShaderUniformInfo uniform, SerializedProperty property)
    {
        if (_material == null) return;
        _material.Properties[uniform.Name] = MaterialShaderPropertyUtils.ConvertSerializedPropertyToMaterialValue(uniform.Type, property);
        SyncRuntimeMaterial();
    }

    private void PersistUniformValuesFromEditors()
    {
        if (_material == null) return;

        foreach (var (uniform, rootProperty) in _uniformProperties)
        {
            ApplyUniformPropertyValue(uniform, rootProperty);
        }
    }

    private void SyncRuntimeMaterial()
    {
        if (_suppressRuntimeSync) return;
        if (_material == null) return;
        if (string.IsNullOrWhiteSpace(AssetId)) return;

        CancelRuntimeSyncDebounce();
        _runtimeSyncDebounceCTS = new CancellationTokenSource();
        var token = _runtimeSyncDebounceCTS.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(RuntimeSyncDebounceDelayMs, token);
                if (token.IsCancellationRequested) return;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested) return;
                    SyncRuntimeMaterialImmediate();
                });
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    private void SyncRuntimeMaterialImmediate()
    {
        if (_suppressRuntimeSync) return;
        if (_material == null) return;
        if (string.IsNullOrWhiteSpace(AssetId)) return;

        var jsonData = JsonConvert.SerializeObject(_material);
        if (_assetRuntimeSyncService.TrySetAssetData(AssetId, jsonData))
        {
            _lastRuntimeJson = jsonData;
        }
    }

    private void CancelRuntimeSyncDebounce()
    {
        _runtimeSyncDebounceCTS?.Cancel();
        _runtimeSyncDebounceCTS?.Dispose();
        _runtimeSyncDebounceCTS = null;
    }

    private void StartRuntimePullLoop()
    {
        CancelRuntimePullLoop();
        _runtimePullTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(RuntimePullIntervalMs)
        };

        _runtimePullTimer.Tick += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(AssetId)) return;
            if (_runtimeSyncDebounceCTS != null) return;

            try
            {
                if (!_assetRuntimeSyncService.TryGetAssetData(AssetId, out var jsonData)) return;
                if (string.IsNullOrWhiteSpace(jsonData)) return;
                if (string.Equals(_lastRuntimeJson, jsonData, StringComparison.Ordinal)) return;

                var runtimeMaterial = JsonConvert.DeserializeObject<Material>(jsonData);
                if (runtimeMaterial == null) return;

                _suppressRuntimeSync = true;
                ApplyRuntimeMaterial(runtimeMaterial);
                var currentMaterial = _material;
                if (currentMaterial == null)
                {
                    _suppressRuntimeSync = false;
                    return;
                }

                _lastRuntimeJson = jsonData;
                ShaderPicker.SyncSelectedAsset(currentMaterial.ShaderAssetId);
                UseDepth = currentMaterial.UseDepth;
                SortingOrder = currentMaterial.SortingOrder;
                RebuildShaderProperties(currentMaterial.ShaderAssetId, new Dictionary<string, object?>(currentMaterial.Properties));
                _suppressRuntimeSync = false;
            }
            catch
            {
                // ignore
            }
        };

        _runtimePullTimer.Start();
    }

    private void CancelRuntimePullLoop()
    {
        if (_runtimePullTimer == null) return;
        _runtimePullTimer.Stop();
        _runtimePullTimer = null;
    }

    private void ApplyRuntimeMaterial(Material runtimeMaterial)
    {
        if (_material == null)
        {
            _material = runtimeMaterial;
            return;
        }

        _material.SetShaderAssetId(runtimeMaterial.ShaderAssetId);
        _material.SetUseDepth(runtimeMaterial.UseDepth);
        _material.SetSortingOrder(runtimeMaterial.SortingOrder);

        _material.Properties.Clear();
        foreach (var (name, value) in runtimeMaterial.Properties)
        {
            _material.Properties[name] = value;
        }
    }
}
