using System;
using System.Linq;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

public static class PropertyViewUtils
{
    public static BaseViewModel CreatePropertyViewModel(
        SerializedProperty property,
        ISerializableObjectsRegistry serializableObjectsRegistry,
        IAssetSearchService assetSearchService,
        IAssetRegistry assetRegistry,
        IAssetTypeMapper assetTypeMapper,
        IProjectAssetFocusService projectAssetFocusService)
    {
        return property.Type switch
        {
            SerializedTypeEnum.Integer => new IntegerPropertyViewModel(property),
            SerializedTypeEnum.String => new StringPropertyViewModel(property),
            SerializedTypeEnum.Boolean => new BooleanPropertyViewModel(property),
            SerializedTypeEnum.Float => new FloatPropertyViewModel(property),
            SerializedTypeEnum.Enum => new EnumPropertyViewModel(property, serializableObjectsRegistry),
            SerializedTypeEnum.Custom => GetPropertyViewModelForCustomType(property, serializableObjectsRegistry, assetSearchService, assetRegistry, assetTypeMapper, projectAssetFocusService),
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
        IProjectAssetFocusService projectAssetFocusService)
    {
        if (property.SourceType == "Vector3")
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

        return new CustomPropertyViewModel(property, serializableObjectsRegistry, assetSearchService, assetRegistry, assetTypeMapper, projectAssetFocusService);
    }
}
