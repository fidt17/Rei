using System;
using Avalonia.Input;

namespace ReiEditor.ViewModels.Windows.Editor.Project.Assets;

public sealed class ProjectAssetItemActions
{
    public Action<ProjectAssetItemViewModel, KeyModifiers> SelectAction { get; }
    public Action<ProjectAssetItemViewModel> ContextMenuSelectAction { get; }
    public Action<ProjectAssetItemViewModel> DeleteAction { get; }
    public Action<ProjectAssetItemViewModel> DuplicateAction { get; }
    public Action<ProjectAssetItemViewModel, string> RenameAction { get; }
    public Action<ProjectAssetItemViewModel> MoveAction { get; }
    public Action<ProjectAssetItemViewModel> OpenAction { get; }

    public ProjectAssetItemActions(
        Action<ProjectAssetItemViewModel, KeyModifiers> selectAction,
        Action<ProjectAssetItemViewModel> contextMenuSelectAction,
        Action<ProjectAssetItemViewModel> deleteAction,
        Action<ProjectAssetItemViewModel> duplicateAction,
        Action<ProjectAssetItemViewModel, string> renameAction,
        Action<ProjectAssetItemViewModel> moveAction,
        Action<ProjectAssetItemViewModel> openAction)
    {
        SelectAction = selectAction;
        ContextMenuSelectAction = contextMenuSelectAction;
        DeleteAction = deleteAction;
        DuplicateAction = duplicateAction;
        RenameAction = renameAction;
        MoveAction = moveAction;
        OpenAction = openAction;
    }
}
