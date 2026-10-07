using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Assets.DataAssets;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Utils;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

public class CustomPropertyViewModel : BaseViewModel
{
    public PropertyNameViewModel PropertyName { get; }
    
    public ObservableCollection<BaseViewModel> Value { get; } = new();
    public ObservableField<bool> Expanded { get; } = new(false);
    
    private readonly List<SerializedProperty> _displayedProperties = new();
    private bool _disposed;
    private readonly SerializedProperty _property;
    private readonly ISerializableObjectsRegistry _serializableObjectsRegistry;
    private readonly IDataAssetTypeRegistry? _dataAssetTypeRegistry;
    private readonly IAssetSearchService _assetSearchService;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IAssetTypeMapper _assetTypeMapper;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IProjectAssetFocusService _projectAssetFocusService;
    private readonly ISceneManagementService _sceneManagementService;
    private readonly ISelectionService _selectionService;

#pragma warning disable CS8618
    public CustomPropertyViewModel() { }
#pragma warning restore CS8618

    public CustomPropertyViewModel(
        SerializedProperty property,
        ISerializableObjectsRegistry serializableObjectsRegistry,
        IAssetSearchService assetSearchService,
        IAssetRegistry assetRegistry,
        IAssetTypeMapper assetTypeMapper,
        IBehaviourRegistry behaviourRegistry,
        IProjectAssetFocusService projectAssetFocusService,
        ISceneManagementService sceneManagementService,
        ISelectionService selectionService,
        IDataAssetTypeRegistry? dataAssetTypeRegistry)
    {
        if (property.Type != SerializedTypeEnum.Custom) throw new Exception($"Invalid property type. Expected {SerializedTypeEnum.Custom}. Actual {property.Type}");
        
        _property = property;
        _serializableObjectsRegistry = serializableObjectsRegistry;
        _dataAssetTypeRegistry = dataAssetTypeRegistry;
        _assetSearchService = assetSearchService;
        _assetRegistry = assetRegistry;
        _assetTypeMapper = assetTypeMapper;
        _behaviourRegistry = behaviourRegistry;
        _projectAssetFocusService = projectAssetFocusService;
        _sceneManagementService = sceneManagementService;
        _selectionService = selectionService;

        PropertyName = new(property);
        _property.ValueChangedEvent += HandlePropertyValueChangedEvent;
        
        HandlePropertyValueChangedEvent(_property.Value);
    }

    public override void Dispose()
    {
        _disposed = true;
        base.Dispose();
        
        _property.ValueChangedEvent -= HandlePropertyValueChangedEvent;
        Value.ClearAndDispose();
        _displayedProperties.Clear();
    }
    
    public void SwitchExpandState() => Expanded.Value = !Expanded.Value;

    private void HandlePropertyValueChangedEvent(object? value)
    {
        Dispatcher.UIThread.Execute(() => HandlePropertyValueChangedEventOnUiThread(value));
    }

    private void HandlePropertyValueChangedEventOnUiThread(object? value)
    {
        if (_disposed) return;
        if (value is null)
        {
            Value.ClearAndDispose();
            _displayedProperties.Clear();
            return;
        }
        
        if (value is Dictionary<string, SerializedProperty> subProperties)
        {
            var registry = _serializableObjectsRegistry;
            var schema = registry?.GetObject(_property.SourceType)?.SerializedProperties;
            var visibleProperties = PropertyDisplayUtils.GetVisibleProperties(subProperties.Values, schema).ToList();
            if (_displayedProperties.SequenceEqual(visibleProperties)) return;

            Value.ClearAndDispose();
            _displayedProperties.Clear();
            
            _displayedProperties.AddRange(visibleProperties);
            foreach (var row in PropertyDisplayUtils.CreateRows(visibleProperties, schema, (property, metadata) =>
                         PropertyViewUtils.CreatePropertyViewModel(property, _serializableObjectsRegistry, _assetSearchService, _assetRegistry, _assetTypeMapper, _behaviourRegistry, _projectAssetFocusService, _sceneManagementService, _selectionService, _dataAssetTypeRegistry, metadata)))
            {
                Value.Add(row);
            }
        }
        else
        {
            throw new Exception($"Not supported value type: {value.GetType()} {value}");
        }
    }
}
