namespace ReiEditor.Models.EditorApp.Selection;

public interface IAssetSelectable : ISelectable
{
    string AssetId { get; }
    string AssetName { get; }
    string AssetPath { get; }
    bool IsAssetSupportedInMonitor { get; }
}
