namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public interface ISelectedEntityDuplicateCommand
{
    SelectedEntityCommandResult Execute(SelectedEntityCommandTarget target);
}
