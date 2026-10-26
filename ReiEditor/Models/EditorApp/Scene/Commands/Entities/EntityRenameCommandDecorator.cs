using System.Threading.Tasks;
using ReiEditor.Models.Services.Commands;

namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public sealed class EntityRenameCommandDecorator : IEntityRenameCommand
{
    private readonly IEntityRenameCommand _command;
    private readonly IEditorCommandService _commands;

    public EntityRenameCommandDecorator(IEntityRenameCommand command, IEditorCommandService commands)
    {
        _command = command;
        _commands = commands;
    }

    public Task<EntityRenameCommandResult> ExecuteAsync(EntityRenameCommandTarget target) => _commands.ExecuteAsync(_command, target);
}
