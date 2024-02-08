namespace ReiEditor.Models.EditorApp.Selection;

public interface ISelectionService
{
    Utils.Common.IObservable<ISelectable?> ActiveSelection { get; }

    void Select(ISelectable selectable);
    void ResetSelection();
}