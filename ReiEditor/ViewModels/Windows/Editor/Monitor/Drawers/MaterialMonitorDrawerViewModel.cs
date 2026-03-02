using System.Threading.Tasks;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Shaders;
using ReiEditor.Models.Services.Render;
using ReiEditor.ViewModels.Controls.Assets;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

public class MaterialMonitorDrawerViewModel : BaseMonitorDrawer
{
    public string AssetName { get; }
    public string AssetId { get; }
    public string AssetIdLabel { get; }
    public AssetPickerViewModel ShaderPicker { get; }

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

    private Material? _material;
    private readonly IAssetsService _assetsService;

#pragma warning disable CS8618
    public MaterialMonitorDrawerViewModel() { }
#pragma warning restore CS8618

    public MaterialMonitorDrawerViewModel(
        IAssetSelectable assetSelection,
        IAssetsService assetsService,
        IShaderRegistry shaderRegistry,
        IAssetRegistry assetRegistry)
    {
        AssetName = assetSelection.AssetName;
        AssetId = assetSelection.AssetId;
        AssetIdLabel = string.IsNullOrWhiteSpace(AssetId) ? "ID: <missing>" : $"ID: {AssetId}";
        _assetsService = assetsService;

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
        ShaderPicker.Dispose();
    }

    private async Task LoadMaterialState()
    {
        if (string.IsNullOrWhiteSpace(AssetId))
        {
            StatusText = "Material id is missing.";
            return;
        }

        _material = await _assetsService.Load<Material>(AssetId);
        if (_material == null)
        {
            StatusText = "Failed to load material asset.";
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ShaderPicker.SyncSelectedAsset(_material.ShaderAssetId);
            StatusText = "";
            IsMaterialLoaded = true;
        });
    }

    private void HandleShaderChanged(string? shaderAssetId, string? _)
    {
        if (_material == null) return;
        _material.SetShaderAssetId(shaderAssetId ?? "");
    }
}
