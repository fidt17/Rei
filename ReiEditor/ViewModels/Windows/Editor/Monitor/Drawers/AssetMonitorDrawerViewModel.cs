using System.IO;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

public class AssetMonitorDrawerViewModel : BaseMonitorDrawer
{
    public string AssetName { get; }
    public string AssetId { get; }
    public string AssetIdLabel { get; }
    public bool ShowAssetIdLabel { get; }
    public bool HasFileContentPreview { get; }
    public string FileContent { get; } = "";

#pragma warning disable CS8618
    public AssetMonitorDrawerViewModel() { }
#pragma warning restore CS8618

    public AssetMonitorDrawerViewModel(IAssetSelectable assetSelection)
    {
        AssetName = assetSelection.AssetName;
        AssetId = assetSelection.AssetId;

        var extension = Path.GetExtension(assetSelection.AssetPath);
        var isCppFile = string.Equals(extension, FileExtensions.CPP, System.StringComparison.OrdinalIgnoreCase);
        var hasAssetId = !string.IsNullOrWhiteSpace(AssetId);
        ShowAssetIdLabel = hasAssetId || !isCppFile;
        AssetIdLabel = hasAssetId ? $"ID: {AssetId}" : "ID: <missing>";

        HasFileContentPreview = AssetMonitorSupportUtility.IsTextPreviewAsset(assetSelection.AssetPath, isDirectory: false);
        if (!HasFileContentPreview) return;

        try
        {
            FileContent = File.ReadAllText(assetSelection.AssetPath);
        }
        catch
        {
            FileContent = "<Could not read file content>";
        }
    }
}
