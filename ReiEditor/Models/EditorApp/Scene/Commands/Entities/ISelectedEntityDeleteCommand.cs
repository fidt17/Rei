namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public interface ISelectedEntityDeleteCommand
{
    SelectedEntityCommandResult Execute(SelectedEntityCommandTarget target);
}
