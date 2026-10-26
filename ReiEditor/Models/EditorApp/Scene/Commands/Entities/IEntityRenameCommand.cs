using ReiEditor.Models.EditorApp.Commands;

namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public interface IEntityRenameCommand : IEditorCommand<EntityRenameCommandTarget, EntityRenameCommandResult>
{
}
