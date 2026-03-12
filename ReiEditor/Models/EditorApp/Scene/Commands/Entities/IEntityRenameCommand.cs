namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public interface IEntityRenameCommand
{
    void Execute(EntityRenameCommandTarget target);
}
