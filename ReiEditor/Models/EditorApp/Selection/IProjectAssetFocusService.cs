using System;

namespace ReiEditor.Models.EditorApp.Selection;

public interface IProjectAssetFocusService
{
    event Action<string>? FocusAssetRequested;
    event Action<string>? FocusAssetPathRequested;

    void FocusAsset(string assetId);
    void FocusAssetPath(string assetPath);
}
