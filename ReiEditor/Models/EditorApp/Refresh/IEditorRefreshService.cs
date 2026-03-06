using System;

namespace ReiEditor.Models.EditorApp.Refresh;

public interface IEditorRefreshService
{
    event Action RefreshedEvent;
    void NotifyRefreshed();
}
