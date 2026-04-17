using System.Collections.Generic;
using Avalonia.Input;

namespace ReiEditor.Models.Services.Scenes;

public interface ISceneAssetDragSessionService
{
    bool CanStart(IReadOnlyList<string> assetPaths);
    void Start(IReadOnlyList<string> assetPaths);
    void HandleDesktopDragCompleted(DragDropEffects result);
    void Cancel();
}
