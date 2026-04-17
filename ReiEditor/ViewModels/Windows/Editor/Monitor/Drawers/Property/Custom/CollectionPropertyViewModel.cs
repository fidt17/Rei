using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Utils;
using ReiEditor.Utils.Common;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Utils;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

public class CollectionPropertyViewModel : BaseViewModel
{
    public PropertyNameViewModel PropertyName { get; }
    public ObservableCollection<CollectionItemViewModel> Value { get; } = new();
    public ObservableField<bool> Expanded { get; } = new(false);
    public RelayCommand ToggleExpandedCommand { get; }
    public RelayCommand AddItemCommand { get; }

    private int _count;
    public int Count
    {
        get => _count;
        private set => SetField(ref _count, value);
    }

    private readonly SerializedProperty _property;
    private readonly ISerializableObjectsRegistry _serializableObjectsRegistry;
    private readonly IAssetSearchService _assetSearchService;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IAssetTypeMapper _assetTypeMapper;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IProjectAssetFocusService _projectAssetFocusService;
    private readonly ISceneManagementService _sceneManagementService;
    private readonly ISelectionService _selectionService;

#pragma warning disable CS8618
    public CollectionPropertyViewModel()
    {
        ToggleExpandedCommand = new RelayCommand();
        AddItemCommand = new RelayCommand();
    }
#pragma warning restore CS8618

    public CollectionPropertyViewModel(
        SerializedProperty property,
        ISerializableObjectsRegistry serializableObjectsRegistry,
        IAssetSearchService assetSearchService,
        IAssetRegistry assetRegistry,
        IAssetTypeMapper assetTypeMapper,
        IBehaviourRegistry behaviourRegistry,
        IProjectAssetFocusService projectAssetFocusService,
        ISceneManagementService sceneManagementService,
        ISelectionService selectionService)
    {
        if (property.Type != SerializedTypeEnum.Collection) throw new Exception($"Invalid property type. Expected {SerializedTypeEnum.Collection}. Actual {property.Type}");

        _property = property;
        _serializableObjectsRegistry = serializableObjectsRegistry;
        _assetSearchService = assetSearchService;
        _assetRegistry = assetRegistry;
        _assetTypeMapper = assetTypeMapper;
        _behaviourRegistry = behaviourRegistry;
        _projectAssetFocusService = projectAssetFocusService;
        _sceneManagementService = sceneManagementService;
        _selectionService = selectionService;

        PropertyName = new PropertyNameViewModel(property);
        ToggleExpandedCommand = new RelayCommand(SwitchExpandState);
        AddItemCommand = new RelayCommand(AddItem);
        _property.ValueChangedEvent += HandlePropertyValueChangedEvent;

        HandlePropertyValueChangedEvent(_property.Value);
    }

    public override void Dispose()
    {
        base.Dispose();

        _property.ValueChangedEvent -= HandlePropertyValueChangedEvent;
        Value.ClearAndDispose();
    }

    public void SwitchExpandState() => Expanded.Value = !Expanded.Value;

    public void AddItem()
    {
        var items = EnsureItems();
        var itemProperty = CreateCollectionItem($"[{items.Count}]", _property);
        items.Add(itemProperty);
        ReindexItems(items);
        _property.NotifyStructureChanged();
    }

    private void HandlePropertyValueChangedEvent(object? value)
    {
        if (value is not List<SerializedProperty> items)
        {
            System.Console.WriteLine($"[MonitorDebug][CollectionProperty] {_property.Name}: clearing items because value is not a collection");
            Value.ClearAndDispose();
            Count = 0;
            return;
        }

        Count = items.Count;

        if (HasSameItems(items))
        {
            System.Console.WriteLine($"[MonitorDebug][CollectionProperty] {_property.Name}: skipping rebuild for {items.Count} unchanged items");
            return;
        }

        System.Console.WriteLine($"[MonitorDebug][CollectionProperty] {_property.Name}: rebuilding {items.Count} items");
        Value.ClearAndDispose();

        foreach (var item in items)
        {
            var itemViewModel = PropertyViewUtils.CreatePropertyViewModel(
                item,
                _serializableObjectsRegistry,
                _assetSearchService,
                _assetRegistry,
                _assetTypeMapper,
                _behaviourRegistry,
                _projectAssetFocusService,
                _sceneManagementService,
                _selectionService);

            Value.Add(new CollectionItemViewModel(item, itemViewModel, () => RemoveItem(item)));
        }
    }

    private bool HasSameItems(List<SerializedProperty> items)
    {
        var currentItems = Value.ToArray();

        if (currentItems.Length != items.Count)
        {
            return false;
        }

        for (var index = 0; index < items.Count; index++)
        {
            if (index >= currentItems.Length || currentItems[index].Property != items[index])
            {
                return false;
            }
        }

        return true;
    }

    private void RemoveItem(SerializedProperty item)
    {
        if (_property.Value is not List<SerializedProperty> items) return;
        if (!items.Remove(item)) return;

        ReindexItems(items);
        _property.NotifyStructureChanged();
    }

    private List<SerializedProperty> EnsureItems()
    {
        if (_property.Value is List<SerializedProperty> items) return items;

        items = new List<SerializedProperty>();
        _property.Value = items;
        return items;
    }

    private void ReindexItems(List<SerializedProperty> items)
    {
        for (var index = 0; index < items.Count; index++)
        {
            items[index].SetName($"[{index}]");
        }
    }

    private SerializedProperty CreateCollectionItem(string name, SerializedProperty parentProperty)
    {
        var itemSourceType = parentProperty.ItemSourceType ?? parentProperty.TemplateTypeName ?? string.Empty;
        var itemType = parentProperty.ItemType == SerializedTypeEnum.Invalid
            ? ResolveType(itemSourceType)
            : parentProperty.ItemType;
        var itemTemplateTypeName = parentProperty.ItemTemplateTypeName ?? SourceFilesUtility.GetTemplateTypeName(itemSourceType);
        var nestedItemType = itemType == SerializedTypeEnum.Collection && itemTemplateTypeName != null
            ? ResolveType(itemTemplateTypeName)
            : SerializedTypeEnum.Invalid;

        var property = new SerializedProperty(
            name,
            itemType,
            GetDefaultValue(itemType, itemSourceType),
            itemSourceType,
            parentProperty,
            itemTemplateTypeName,
            nestedItemType,
            itemType == SerializedTypeEnum.Collection ? itemTemplateTypeName : null,
            itemType == SerializedTypeEnum.Collection && itemTemplateTypeName != null ? SourceFilesUtility.GetTemplateTypeName(itemTemplateTypeName) : null);

        if (itemType == SerializedTypeEnum.Custom)
        {
            var serializableObject = _serializableObjectsRegistry.GetObject(itemSourceType);
            if (serializableObject != null)
            {
                var nestedProperties = new Dictionary<string, SerializedProperty>();
                foreach (var child in serializableObject.SerializedProperties)
                {
                    nestedProperties[child.Key] = CreatePropertyFromData(child.Key, child.Value, property);
                }
                property.Value = nestedProperties;
            }
        }
        else if (itemType == SerializedTypeEnum.Collection)
        {
            property.Value = new List<SerializedProperty>();
        }

        return property;
    }

    private SerializedProperty CreatePropertyFromData(string name, SerializableObjectInfo.SerializedPropertyData propertyData, SerializedProperty parentProperty)
    {
        var propertyType = propertyData.Type == SerializedTypeEnum.Custom
            ? ResolveType(propertyData.SourceType)
            : propertyData.Type;
        var templateTypeName = propertyData.TemplateTypeName ?? SourceFilesUtility.GetTemplateTypeName(propertyData.SourceType);
        var property = new SerializedProperty(
            name,
            propertyType,
            GetDefaultValue(propertyType, propertyData.SourceType),
            propertyData.SourceType,
            parentProperty,
            templateTypeName,
            propertyData.ItemType,
            propertyData.ItemSourceType,
            propertyData.ItemTemplateTypeName);

        if (propertyType == SerializedTypeEnum.Custom)
        {
            var serializableObject = _serializableObjectsRegistry.GetObject(propertyData.SourceType);
            if (serializableObject != null)
            {
                var nestedProperties = new Dictionary<string, SerializedProperty>();
                foreach (var child in serializableObject.SerializedProperties)
                {
                    nestedProperties[child.Key] = CreatePropertyFromData(child.Key, child.Value, property);
                }
                property.Value = nestedProperties;
            }
        }
        else if (propertyType == SerializedTypeEnum.Collection)
        {
            property.Value = new List<SerializedProperty>();
        }

        return property;
    }

    private object? GetDefaultValue(SerializedTypeEnum type, string sourceType)
    {
        if (type == SerializedTypeEnum.Collection) return new List<SerializedProperty>();

        if (type == SerializedTypeEnum.Custom) return null;

        if (type == SerializedTypeEnum.Enum)
        {
            var enumName = SerializedTypeNameParser.GetBaseTypeName(sourceType);
            var enumInfo = _serializableObjectsRegistry.GetEnum(enumName);
            if (enumInfo == null || enumInfo.Options.Count == 0) return 0;
            return enumInfo.Options.First().Value;
        }

        return type.GetDefaultValue();
    }

    private SerializedTypeEnum ResolveType(string sourceType)
    {
        var normalizedType = SerializedTypeNameParser.NormalizeSourceType(sourceType);
        var baseTypeName = SerializedTypeNameParser.GetBaseTypeName(normalizedType);

        if (baseTypeName is "int" or "i32" or "u32") return SerializedTypeEnum.Integer;

        if (baseTypeName is "string") return SerializedTypeEnum.String;

        if (baseTypeName is "bool") return SerializedTypeEnum.Boolean;

        if (baseTypeName is "float" or "f32" or "double") return SerializedTypeEnum.Float;

        if (baseTypeName is "vector") return SerializedTypeEnum.Collection;

        if (_serializableObjectsRegistry.GetEnum(baseTypeName) != null) return SerializedTypeEnum.Enum;

        return SerializedTypeEnum.Custom;
    }
}
