using System;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.ViewModels.Controls.Assets;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

public class AssetPropertyViewModel : BaseCustomPropertyViewModel
{
    public AssetPickerViewModel? AssetPicker { get; private set; }

    private bool _isInitialized;

#pragma warning disable CS8618
    public AssetPropertyViewModel() { }
#pragma warning restore CS8618

    public AssetPropertyViewModel(
        SerializedProperty property,
        IAssetSearchService assetSearchService,
        IAssetRegistry assetRegistry,
        IAssetTypeMapper assetTypeMapper) : base(property)
    {
        if (property.Type != SerializedTypeEnum.Custom) throw new Exception($"Invalid property type. Expected {SerializedTypeEnum.Custom}. Actual {property.Type}");

        var templateTypeName = property.TemplateTypeName;
        var assetType = assetTypeMapper.GetAssetTypeForTemplateType(templateTypeName);
        AssetPicker = new AssetPickerViewModel(
            assetSearchService,
            assetRegistry,
            assetTypeMapper.GetExtensionsForAssetType(assetType),
            (assetId, _) => SelectAsset(assetId));

        var idProperty = GetNestedProperty("Id");
        if (idProperty != null)
        {
            idProperty.ValueChangedEvent += HandleIdValueChangedEvent;
        }

        _isInitialized = true;
        AssetPicker.SyncSelectedAsset(GetAssetId());
    }

    public override void Dispose()
    {
        base.Dispose();
        AssetPicker?.Dispose();
        var idProperty = GetNestedProperty("Id");
        if (idProperty != null)
        {
            idProperty.ValueChangedEvent -= HandleIdValueChangedEvent;
        }
    }

    protected override void HandlePropertyValueChangedEvent(object? value)
    {
        if (!_isInitialized) return;
        AssetPicker?.SyncSelectedAsset(GetAssetId());
    }

    private void HandleIdValueChangedEvent(object? value)
    {
        if (!_isInitialized) return;
        AssetPicker?.SyncSelectedAsset(value as string);
    }

    private string? GetAssetId()
    {
        var idProperty = GetNestedProperty("Id");
        return idProperty?.Value as string;
    }

    private void SelectAsset(string? assetId)
    {
        var idProperty = GetNestedProperty("Id");
        if (idProperty == null) return;

        idProperty.Value = assetId ?? "";
    }
}
