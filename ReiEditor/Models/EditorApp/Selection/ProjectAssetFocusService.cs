using System;

namespace ReiEditor.Models.EditorApp.Selection;

public class ProjectAssetFocusService : IProjectAssetFocusService
{
    public event Action<string>? FocusAssetRequested;

    public void FocusAsset(string assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId)) return;

        FocusAssetRequested?.Invoke(assetId);
    }
}
