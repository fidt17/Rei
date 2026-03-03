using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Assets.Shaders;
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

    private Material? _material;
    private readonly IAssetsService _assetsService;
    private readonly IShaderRegistry _shaderRegistry;
    private readonly IAssetSearchService _assetSearchService;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IAssetTypeMapper _assetTypeMapper;
    private readonly List<(SerializedProperty Property, Action<object?> Handler)> _propertySubscriptions = new();
    private readonly List<(ShaderUniformInfo Uniform, SerializedProperty RootProperty)> _uniformProperties = new();

#pragma warning disable CS8618
    public MaterialMonitorDrawerViewModel() { }
#pragma warning restore CS8618

    public MaterialMonitorDrawerViewModel(
        IAssetSelectable assetSelection,
        IAssetsService assetsService,
        IAssetSearchService assetSearchService,
        IShaderRegistry shaderRegistry,
        IAssetRegistry assetRegistry,
        IAssetTypeMapper assetTypeMapper)
    {
        AssetName = assetSelection.AssetName;
        AssetId = assetSelection.AssetId;
        AssetIdLabel = string.IsNullOrWhiteSpace(AssetId) ? "ID: <missing>" : $"ID: {AssetId}";
        _assetsService = assetsService;
        _assetSearchService = assetSearchService;
        _shaderRegistry = shaderRegistry;
        _assetRegistry = assetRegistry;
        _assetTypeMapper = assetTypeMapper;

        ShaderPicker = new AssetPickerViewModel(
            assetRegistry,
            shaderRegistry.BuildEntries(),
            HandleShaderChanged);
        ShaderPicker.RefreshSearchResultsForAll();

        _ = LoadMaterialState();
    }

    public override void Dispose()
    {
        base.Dispose();
        PersistUniformValuesFromEditors();
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
                ShaderPicker.SyncSelectedAsset(_material.ShaderAssetId);
                RebuildShaderProperties(_material.ShaderAssetId, new Dictionary<string, object?>(_material.Properties));
                StatusText = "";
                IsMaterialLoaded = true;
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
            ShaderUniformType.Texture => new AssetPropertyViewModel(property, _assetSearchService, _assetRegistry, _assetTypeMapper),
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
    }

    private void PersistUniformValuesFromEditors()
    {
        if (_material == null) return;

        foreach (var (uniform, rootProperty) in _uniformProperties)
        {
            ApplyUniformPropertyValue(uniform, rootProperty);
        }
    }
}
