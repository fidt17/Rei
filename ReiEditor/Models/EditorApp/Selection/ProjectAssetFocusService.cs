using System;

namespace ReiEditor.Models.EditorApp.Selection;

public class ProjectAssetFocusService : IProjectAssetFocusService
{
    public event Action<string>? SelectAssetRequested;

    public void SelectAsset(string assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId)) return;
        SelectAssetRequested?.Invoke(assetId);
    }

    public event Action<string>? FocusAssetRequested;
    public event Action<string>? FocusAssetPathRequested;

    public void FocusAsset(string assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId)) return;

        FocusAssetRequested?.Invoke(assetId);
    }

    public void FocusAssetPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath)) return;

        FocusAssetPathRequested?.Invoke(assetPath);
    }
}
