using ReiEditor.Utils.Common;

namespace ReiEditor.Models.EditorApp.ProjectBuildWindow;

public interface IProjectBuildWindowService
{
    IObservable<bool> IsOpened { get; }

    void OpenWindow();
    void CloseWindow();
}
