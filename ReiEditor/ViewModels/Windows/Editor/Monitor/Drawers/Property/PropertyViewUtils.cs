using System;
using System.Linq;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
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
        ISelectionService selectionService)
    {
        return property.Type switch
        {
            SerializedTypeEnum.Integer => new IntegerPropertyViewModel(property),
            SerializedTypeEnum.String => new StringPropertyViewModel(property),
            SerializedTypeEnum.Boolean => new BooleanPropertyViewModel(property),
            SerializedTypeEnum.Float => new FloatPropertyViewModel(property),
            SerializedTypeEnum.Enum => new EnumPropertyViewModel(property, serializableObjectsRegistry),
            SerializedTypeEnum.Collection => new CollectionPropertyViewModel(property, serializableObjectsRegistry, assetSearchService, assetRegistry, assetTypeMapper, behaviourRegistry, projectAssetFocusService, sceneManagementService, selectionService),
            SerializedTypeEnum.Custom => GetPropertyViewModelForCustomType(property, serializableObjectsRegistry, assetSearchService, assetRegistry, assetTypeMapper, behaviourRegistry, projectAssetFocusService, sceneManagementService, selectionService),
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
        ISelectionService selectionService)
    {
        if (property.SourceType == "Vector2")
        {
            return new Vector2PropertyViewModel(property);
        }
        else if (property.SourceType == "Vector3")
        {
            return new Vector3PropertyViewModel(property);
        }
        else if (property.SourceType == "Color")
        {
            return new ColorPropertyViewModel(property);
        }
        else if (property.SourceType.StartsWith("AssetRef<", StringComparison.Ordinal))
        {
            return new AssetPropertyViewModel(property, assetSearchService, assetRegistry, assetTypeMapper, projectAssetFocusService);
        }
        else if (property.SourceType.StartsWith("ComponentRef<", StringComparison.Ordinal))
        {
            return new ComponentRefPropertyViewModel(property, assetRegistry, behaviourRegistry, sceneManagementService, selectionService);
        }

        return new CustomPropertyViewModel(property, serializableObjectsRegistry, assetSearchService, assetRegistry, assetTypeMapper, behaviourRegistry, projectAssetFocusService, sceneManagementService, selectionService);
    }
}
