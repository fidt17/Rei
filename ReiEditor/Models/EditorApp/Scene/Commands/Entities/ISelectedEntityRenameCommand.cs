namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public interface ISelectedEntityRenameCommand
{
    SelectedEntityCommandResult Execute(SelectedEntityCommandTarget target);
}
