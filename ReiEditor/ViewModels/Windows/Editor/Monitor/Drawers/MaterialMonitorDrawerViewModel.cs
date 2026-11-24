using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.EditorApp.Project.Commands.Assets;
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
            if (_disposed || _useDepth == value) return;
            if (_setProperty != null && _material is { } material)
            {
                CommitProperty(() => _setProperty.SetUseDepthAsync(material, value));
                return;
            }
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
            if (_disposed || _sortingOrder == value) return;
            if (_setProperty != null && _material is { } material)
            {
                CommitProperty(() => _setProperty.SetSortingOrderAsync(material, value));
                return;
            }
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
    private readonly SetMaterialPropertyCommand? _setProperty;
    private readonly List<(SerializedProperty Property, Action<object?> Handler)> _propertySubscriptions = new();
    private readonly List<(ShaderUniformInfo Uniform, SerializedProperty RootProperty)> _uniformProperties = new();
    private (string Name, JToken? Before)? _colorPreview;
    private ShaderUniformInfo[]? _shaderUniforms;
    private CancellationTokenSource? _runtimeSyncDebounceCTS;
    private DispatcherTimer? _runtimePullTimer;
    private bool _suppressRuntimeSync;
    private bool _disposed;
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
        IProjectAssetFocusService projectAssetFocusService,
        SetMaterialPropertyCommand? setProperty = null)
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
        _setProperty = setProperty;
        if (_setProperty != null) _setProperty.PropertyChanged += RefreshProperty;

        ShaderPicker = new AssetPickerViewModel(
            assetRegistry,
            shaderRegistry.BuildEntries(),
            HandleShaderChanged);
        ShaderPicker.RefreshSearchResultsForAll();

        _ = LoadMaterialState();
        StartRuntimePullLoop();
    }

    internal bool TryRefreshAsset(Asset asset)
    {
        if (!IsMaterialLoaded || _material is not { } material || !ReferenceEquals(material, asset)) return false;

        var currentUniforms = _shaderRegistry.TryGetById(material.ShaderAssetId, out var shader) ? shader.Uniforms : null;
        var shaderChanged = ShaderPicker.SelectedAssetId != material.ShaderAssetId
            || !SameUniforms(_shaderUniforms, currentUniforms);
        RefreshMaterial(material, shaderChanged);
        if (!shaderChanged) RefreshUniformValues(material);
        return true;
    }

    private static bool SameUniforms(IReadOnlyList<ShaderUniformInfo>? previous, IReadOnlyList<ShaderUniformInfo>? current)
    {
        if (previous == null || current == null) return previous == current;
        return previous.Select(uniform => (uniform.Name, uniform.Type, uniform.SourceType))
            .SequenceEqual(current.Select(uniform => (uniform.Name, uniform.Type, uniform.SourceType)));
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        base.Dispose();
        if (_setProperty != null) _setProperty.PropertyChanged -= RefreshProperty;
        CancelRuntimePullLoop();
        if (_runtimeSyncDebounceCTS != null)
        {
            CancelRuntimeSyncDebounce();
            SyncRuntimeMaterialImmediate();
        }
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
                    if (_disposed) return;
                    StatusText = "Material id is missing.";
                    IsMaterialLoaded = false;
                });
                return;
            }

            var material = await _assetsService.Load<Material>(AssetId);
            if (_disposed) return;
            _material = material;
            if (material == null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_disposed) return;
                    StatusText = "Failed to load material asset.";
                    IsMaterialLoaded = false;
                });
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_disposed) return;
                RefreshMaterial(material, refreshShader: true);
                StatusText = "";
                IsMaterialLoaded = true;
            });
        }
        catch (Exception e)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_disposed) return;
                StatusText = $"Failed to load material asset. {e.Message}";
                IsMaterialLoaded = false;
            });
        }
    }

    private void HandleShaderChanged(string? shaderAssetId, string? _fullPath)
    {
        if (_disposed || _material is not { } material) return;

        var targetShaderAssetId = shaderAssetId ?? "";
        if (string.Equals(material.ShaderAssetId, targetShaderAssetId, StringComparison.Ordinal)) return;

        if (_setProperty != null)
        {
            CommitProperty(() => _setProperty.SetShaderAsync(material, targetShaderAssetId, PrepareShaderProperties(material, targetShaderAssetId)));
            ShaderPicker.SyncSelectedAsset(material.ShaderAssetId);
            return;
        }

        material.SetShader(targetShaderAssetId, PrepareShaderProperties(material, targetShaderAssetId));
        RefreshShader(material);
        SyncRuntimeMaterial();
    }

    private Dictionary<string, object?> PrepareShaderProperties(Material material, string shaderId)
    {
        var values = new Dictionary<string, object?>();
        if (!_shaderRegistry.TryGetById(shaderId, out var shader)) return values;

        foreach (var uniform in shader.Uniforms.Where(uniform => uniform.IsSupported))
        {
            var previousValue = material.Properties.GetValueOrDefault(uniform.Name);
            var property = MaterialShaderPropertyUtils.CreateSerializedProperty(uniform, previousValue);
            values[uniform.Name] = MaterialShaderPropertyUtils.ConvertSerializedPropertyToMaterialValue(uniform.Type, property);
        }

        return values;
    }

    private void RefreshShader(Material material)
    {
        ShaderPicker.SyncSelectedAsset(material.ShaderAssetId);
        UnsubscribeFromUniformPropertyChanges();
        ShaderProperties.ClearAndDispose();
        _uniformProperties.Clear();
        _colorPreview = null;
        _shaderUniforms = null;

        if (string.IsNullOrWhiteSpace(material.ShaderAssetId))
        {
            HasShaderProperties = false;
            ShaderPropertiesStatusText = "Material shader is not set.";
            return;
        }

        if (!_shaderRegistry.TryGetById(material.ShaderAssetId, out var shader))
        {
            HasShaderProperties = false;
            ShaderPropertiesStatusText = "Selected shader asset was not found.";
            return;
        }

        _shaderUniforms = shader.Uniforms.ToArray();
        foreach (var uniform in _shaderUniforms.Where(x => x.IsSupported))
        {
            try
            {
                var rootProperty = MaterialShaderPropertyUtils.CreateSerializedProperty(uniform, material.Properties.GetValueOrDefault(uniform.Name));
                var viewModel = CreatePropertyViewModel(uniform, rootProperty);
                if (viewModel == null) continue;

                ShaderProperties.Add(viewModel);
                _uniformProperties.Add((uniform, rootProperty));
                SubscribeToUniformPropertyChanges(uniform, rootProperty);
            }
            catch
            {
                // Preserve serialized values when a uniform editor cannot be constructed.
            }
        }

        HasShaderProperties = ShaderProperties.Count > 0;
        ShaderPropertiesStatusText = HasShaderProperties
            ? ""
            : "Selected shader has no supported editable uniforms.";
    }

    private BaseViewModel? CreatePropertyViewModel(ShaderUniformInfo uniform, SerializedProperty property)
    {
        Action<object>? commit = _setProperty == null ? null : value => CommitUniform(uniform, value);
        Action<SerializedColorValue>? recordColorDrag = _setProperty == null ? null : _ => RecordColorDrag(uniform.Name);
        return uniform.Type switch
        {
            ShaderUniformType.Float => new FloatPropertyViewModel(property, commit),
            ShaderUniformType.Integer => new IntegerPropertyViewModel(property, commit),
            ShaderUniformType.Color => new ColorPropertyViewModel(property, commit, recordColorDrag),
            ShaderUniformType.Texture => new AssetPropertyViewModel(property, _assetSearchService, _assetRegistry, _assetTypeMapper, _projectAssetFocusService, commitProperty: commit),
            _ => null
        };
    }

    private void CommitUniform(ShaderUniformInfo uniform, object value)
    {
        if (_disposed || _material is not { } material || _setProperty == null) return;
        CommitProperty(() => _setProperty.SetUniformAsync(material, uniform.Name, ToMaterialValue(uniform.Type, value)));
    }

    private async void CommitProperty(Func<Task<SetMaterialPropertyCommand.Result?>> edit)
    {
        try
        {
            await edit();
        }
        catch (Exception exception)
        {
            if (!_disposed) StatusText = $"Failed to edit material. {exception.Message}";
        }
    }

    private void RecordColorDrag(string name)
    {
        if (_disposed || _material == null || _setProperty == null) return;
        if (_colorPreview is not { } preview || preview.Name != name) return;
        _colorPreview = null;
        _setProperty.RecordApplied(_material, name, preview.Before);
        if (_runtimeSyncDebounceCTS == null) return;
        CancelRuntimeSyncDebounce();
        SyncRuntimeMaterialImmediate();
    }

    private static object ToMaterialValue(ShaderUniformType type, object value) => value switch
    {
        SerializedColorValue color => new Dictionary<string, object?> { ["r"] = color.R, ["g"] = color.G, ["b"] = color.B, ["a"] = color.A },
        string id when type == ShaderUniformType.Texture => new Dictionary<string, object?> { ["Id"] = id },
        _ => value
    };

    private void RefreshProperty(Material material, SetMaterialPropertyCommand.Property kind, string? uniformName, string? runtimeJson)
    {
        if (_disposed || !ReferenceEquals(_material, material)) return;
        if (runtimeJson != null)
        {
            CancelRuntimeSyncDebounce();
            _lastRuntimeJson = runtimeJson;
        }

        if (kind == SetMaterialPropertyCommand.Property.Shader)
        {
            RefreshShader(material);
            return;
        }

        if (kind != SetMaterialPropertyCommand.Property.Uniform)
        {
            RefreshRenderSettings(material);
            return;
        }

        var (uniform, root) = _uniformProperties.Find(row => row.Uniform.Name == uniformName);
        if (uniform != null) RefreshUniform(material, uniform, root);
    }

    private void RefreshUniform(Material material, ShaderUniformInfo uniform, SerializedProperty root)
    {
        var value = MaterialShaderPropertyUtils.CreateSerializedProperty(uniform, material.Properties.GetValueOrDefault(uniform.Name)).Value;
        _suppressRuntimeSync = true;
        try
        {
            if (root.Value is Dictionary<string, SerializedProperty> children && value is Dictionary<string, SerializedProperty> updated)
            {
                if (updated.All(field => Equals(children[field.Key].Value, field.Value.Value))) return;
                foreach (var (field, child) in updated) children[field].SetValueWithoutTriggeringChangedEvent(child.Value!);
            }
            else
            {
                if (Equals(root.Value, value)) return;
                root.SetValueWithoutTriggeringChangedEvent(value!);
            }

            root.TriggerChangedEvent();
        }
        finally { _suppressRuntimeSync = false; }
    }

    private void RefreshUniformValues(Material material)
    {
        foreach (var (uniform, property) in _uniformProperties) RefreshUniform(material, uniform, property);
    }

    private void RefreshRenderSettings(Material material)
    {
        SetField(ref _useDepth, material.UseDepth, nameof(UseDepth));
        SetField(ref _sortingOrder, material.SortingOrder, nameof(SortingOrder));
    }

    private void RefreshMaterial(Material material, bool refreshShader)
    {
        _suppressRuntimeSync = true;
        try
        {
            RefreshRenderSettings(material);
            if (refreshShader) RefreshShader(material);
        }
        finally { _suppressRuntimeSync = false; }
    }

    private void SubscribeToUniformPropertyChanges(ShaderUniformInfo uniform, SerializedProperty rootProperty)
    {
        var previewColor = _setProperty != null && uniform.Type == ShaderUniformType.Color;
        var observedProperties = previewColor
            ? new[] { rootProperty }
            : MaterialShaderPropertyUtils.GetObservedProperties(uniform.Type, rootProperty);
        foreach (var observedProperty in observedProperties)
        {
            Action<object?> handler = _ => ApplyUniformPropertyValue(uniform, rootProperty, previewColor);
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

    private void ApplyUniformPropertyValue(ShaderUniformInfo uniform, SerializedProperty property, bool previewColor = false)
    {
        if (_disposed || _suppressRuntimeSync || _material == null) return;
        if (previewColor) _colorPreview ??= (uniform.Name, SetMaterialPropertyCommand.ReadUniform(_material, uniform.Name));
        _material.Properties[uniform.Name] = MaterialShaderPropertyUtils.ConvertSerializedPropertyToMaterialValue(uniform.Type, property);
        SyncRuntimeMaterial(keepPending: previewColor);
    }

    private void SyncRuntimeMaterial(bool keepPending = false)
    {
        if (_disposed) return;
        if (_suppressRuntimeSync) return;
        if (_material == null) return;
        if (string.IsNullOrWhiteSpace(AssetId)) return;
        if (keepPending && _runtimeSyncDebounceCTS != null) return;

        CancelRuntimeSyncDebounce();
        var source = new CancellationTokenSource();
        _runtimeSyncDebounceCTS = source;
        var token = source.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(RuntimeSyncDebounceDelayMs, token);
                if (token.IsCancellationRequested) return;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_disposed || token.IsCancellationRequested) return;
                    SyncRuntimeMaterialImmediate();
                });
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!ReferenceEquals(_runtimeSyncDebounceCTS, source)) return;
                    _runtimeSyncDebounceCTS = null;
                    source.Dispose();
                });
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
        var source = Interlocked.Exchange(ref _runtimeSyncDebounceCTS, null);
        source?.Cancel();
        source?.Dispose();
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
            if (_disposed || !IsMaterialLoaded || _material is not { } material) return;
            if (string.IsNullOrWhiteSpace(AssetId)) return;
            if (_runtimeSyncDebounceCTS != null) return;

            try
            {
                if (!_assetRuntimeSyncService.TryGetAssetData(AssetId, out var jsonData)) return;
                if (string.IsNullOrWhiteSpace(jsonData)) return;
                if (string.Equals(_lastRuntimeJson, jsonData, StringComparison.Ordinal)) return;

                var runtimeMaterial = JsonConvert.DeserializeObject<Material>(jsonData);
                if (runtimeMaterial == null) return;

                _lastRuntimeJson = jsonData;
                var shaderChanged = material.ShaderAssetId != runtimeMaterial.ShaderAssetId;
                var uniformsChanged = shaderChanged
                    || !JToken.DeepEquals(JObject.FromObject(material.Properties), JObject.FromObject(runtimeMaterial.Properties));
                if (uniformsChanged) material.SetShader(runtimeMaterial.ShaderAssetId, runtimeMaterial.Properties);
                material.SetUseDepth(runtimeMaterial.UseDepth);
                material.SetSortingOrder(runtimeMaterial.SortingOrder);
                RefreshMaterial(material, shaderChanged);
                if (!shaderChanged && uniformsChanged) RefreshUniformValues(material);
            }
            catch
            {
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
}
