using System;
using Avalonia.Threading;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Controls.Assets;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

public class AssetPropertyViewModel : BaseCustomPropertyViewModel
{
    public AssetPickerViewModel? AssetPicker { get; private set; }

    private bool _isInitialized;
    private readonly IProjectAssetFocusService? _projectAssetFocusService;

#pragma warning disable CS8618
    public AssetPropertyViewModel() { }
#pragma warning restore CS8618

    public AssetPropertyViewModel(
        SerializedProperty property,
        IAssetSearchService assetSearchService,
        IAssetRegistry assetRegistry,
        IAssetTypeMapper assetTypeMapper,
        IProjectAssetFocusService projectAssetFocusService) : base(property)
    {
        if (property.Type != SerializedTypeEnum.Custom) throw new Exception($"Invalid property type. Expected {SerializedTypeEnum.Custom}. Actual {property.Type}");

        _projectAssetFocusService = projectAssetFocusService;

        var templateTypeName = property.TemplateTypeName;
        var assetType = assetTypeMapper.GetAssetTypeForTemplateType(templateTypeName);
        AssetPicker = new AssetPickerViewModel(
            assetSearchService,
            assetRegistry,
            assetTypeMapper.GetExtensionsForAssetType(assetType),
            (assetId, _) => SelectAsset(assetId));
        AssetPicker.AssetActivatedEvent += HandleAssetActivatedEvent;

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
        if (AssetPicker != null)
        {
            AssetPicker.AssetActivatedEvent -= HandleAssetActivatedEvent;
        }

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
        Dispatcher.UIThread.Execute(() => AssetPicker?.SyncSelectedAsset(ConvertToString(value)));
    }

    private string? GetAssetId()
    {
        var idProperty = GetNestedProperty("Id");
        return ConvertToString(idProperty?.Value);
    }

    private void SelectAsset(string? assetId)
    {
        var idProperty = GetNestedProperty("Id");
        if (idProperty == null) return;

        idProperty.Value = assetId ?? "";
    }

    private void HandleAssetActivatedEvent()
    {
        if (!_isInitialized) return;

        var assetId = GetAssetId();
        if (string.IsNullOrWhiteSpace(assetId)) return;

        _projectAssetFocusService?.FocusAsset(assetId);
    }

    private static string? ConvertToString(object? value)
    {
        if (value is null) return null;
        if (value is JToken token) value = token.ToObject<object?>();
        return value as string ?? value?.ToString();
    }
}
