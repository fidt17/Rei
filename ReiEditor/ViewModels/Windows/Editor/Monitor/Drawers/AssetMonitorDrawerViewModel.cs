using ReiEditor.Models.EditorApp.Selection;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

public class AssetMonitorDrawerViewModel : BaseMonitorDrawer
{
    public string AssetName { get; }
    public string AssetId { get; }
    public string AssetIdLabel { get; }

#pragma warning disable CS8618
    public AssetMonitorDrawerViewModel() { }
#pragma warning restore CS8618

    public AssetMonitorDrawerViewModel(IAssetSelectable assetSelection)
    {
        AssetName = assetSelection.AssetName;
        AssetId = assetSelection.AssetId;
        AssetIdLabel = string.IsNullOrWhiteSpace(AssetId) ? "ID: <missing>" : $"ID: {AssetId}";
    }
}
