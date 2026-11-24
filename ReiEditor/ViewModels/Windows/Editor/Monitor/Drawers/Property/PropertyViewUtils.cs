using System;
using System.Linq;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Assets.DataAssets;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.Collection;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.Vector;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

public static class PropertyViewUtils
{
    public static BaseViewModel CreatePropertyViewModel(
        SerializedProperty property,
        ISerializableObjectsRegistry serializableObjectsRegistry,
        IAssetSearchService assetSearchService,
        IAssetRegistry assetRegistry,
        IAssetTypeMapper assetTypeMapper,
        IBehaviourRegistry behaviourRegistry,
        IProjectAssetFocusService projectAssetFocusService,
        ISceneManagementService sceneManagementService,
        ISelectionService selectionService,
        IDataAssetTypeRegistry? dataAssetTypeRegistry = null,
        SerializableObjectInfo.SerializedPropertyData? metadata = null, Action<SerializedProperty, object>? commitProperty = null,
        Action<SerializedColorValue>? recordColorDrag = null, Action<SerializedProperty, object>? recordRangeDrag = null)
    {
        Action<object>? commit = commitProperty == null ? null : value => commitProperty(property, value);
        if (metadata?.Range is { } range)
            return new RangePropertyViewModel(property, range, commit, recordRangeDrag == null ? null : before => recordRangeDrag(property, before));

        return property.Type switch
        {
            SerializedTypeEnum.Integer => new IntegerPropertyViewModel(property, commit),
            SerializedTypeEnum.String => new StringPropertyViewModel(property, commit),
            SerializedTypeEnum.Boolean => new BooleanPropertyViewModel(property, commit),
            SerializedTypeEnum.Float => new FloatPropertyViewModel(property, commit),
            SerializedTypeEnum.Enum => new EnumPropertyViewModel(property, serializableObjectsRegistry, commit),
            SerializedTypeEnum.Collection => new CollectionPropertyViewModel(property, serializableObjectsRegistry, assetSearchService, assetRegistry, assetTypeMapper, behaviourRegistry, projectAssetFocusService, sceneManagementService, selectionService, dataAssetTypeRegistry, commitProperty),
            SerializedTypeEnum.Custom => GetPropertyViewModelForCustomType(property, serializableObjectsRegistry, assetSearchService, assetRegistry, assetTypeMapper, behaviourRegistry, projectAssetFocusService, sceneManagementService, selectionService, dataAssetTypeRegistry, commitProperty, recordColorDrag),
            SerializedTypeEnum.Invalid => throw new ArgumentOutOfRangeException(),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
    
    public static string ConvertPropertyNameToEditorStyle(string original)
    {
        var charList = original.ToCharArray().ToList();
        if (charList[0] == '_')
        {
            charList.RemoveAt(0);
        }
        
        charList[0] = char.ToUpper(charList[0]);

        return new string(charList.ToArray());
    }

    private static BaseViewModel GetPropertyViewModelForCustomType(
        SerializedProperty property,
        ISerializableObjectsRegistry serializableObjectsRegistry,
        IAssetSearchService assetSearchService,
        IAssetRegistry assetRegistry,
        IAssetTypeMapper assetTypeMapper,
        IBehaviourRegistry behaviourRegistry,
        IProjectAssetFocusService projectAssetFocusService,
        ISceneManagementService sceneManagementService,
        ISelectionService selectionService,
        IDataAssetTypeRegistry? dataAssetTypeRegistry,
        Action<SerializedProperty, object>? commitProperty,
        Action<SerializedColorValue>? recordColorDrag)
    {
        Action<object>? commit = commitProperty == null ? null : value => commitProperty(property, value);
        if (property.SourceType == "Vector2")
        {
            return new Vector2PropertyViewModel(property, commit);
        }
        else if (property.SourceType == "Vector3")
        {
            return new Vector3PropertyViewModel(property, commit);
        }
        else if (property.SourceType == "Color")
        {
            return new ColorPropertyViewModel(property, commit, recordColorDrag);
        }
        else if (property.SourceType.IsAssetReference())
        {
            return new AssetPropertyViewModel(property, assetSearchService, assetRegistry, assetTypeMapper, projectAssetFocusService, dataAssetTypeRegistry, commit);
        }
        else if (property.SourceType.IsComponentReference())
        {
            return new ComponentRefPropertyViewModel(property, assetRegistry, behaviourRegistry, sceneManagementService, selectionService, commit);
        }

        return new CustomPropertyViewModel(property, serializableObjectsRegistry, assetSearchService, assetRegistry, assetTypeMapper, behaviourRegistry, projectAssetFocusService, sceneManagementService, selectionService, dataAssetTypeRegistry, commitProperty);
    }
}
