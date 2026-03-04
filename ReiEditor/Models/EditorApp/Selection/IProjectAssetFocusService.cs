using System;

namespace ReiEditor.Models.EditorApp.Selection;

public interface IProjectAssetFocusService
{
    event Action<string>? FocusAssetRequested;

    void FocusAsset(string assetId);
}
