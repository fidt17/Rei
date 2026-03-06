using System;

namespace ReiEditor.ViewModels.Windows.Editor.Project.Assets;

public sealed class ProjectAssetItemActions
{
    public Action<ProjectAssetItemViewModel> DeleteAction { get; }
    public Action<ProjectAssetItemViewModel> DuplicateAction { get; }
    public Action<ProjectAssetItemViewModel, string> RenameAction { get; }
    public Action<ProjectAssetItemViewModel> MoveAction { get; }
    public Action<ProjectAssetItemViewModel> OpenAction { get; }

    public ProjectAssetItemActions(
        Action<ProjectAssetItemViewModel> deleteAction,
        Action<ProjectAssetItemViewModel> duplicateAction,
        Action<ProjectAssetItemViewModel, string> renameAction,
        Action<ProjectAssetItemViewModel> moveAction,
        Action<ProjectAssetItemViewModel> openAction)
    {
        DeleteAction = deleteAction;
        DuplicateAction = duplicateAction;
        RenameAction = renameAction;
        MoveAction = moveAction;
        OpenAction = openAction;
    }
}
