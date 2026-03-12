using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public class SelectedEntityRenameCommand : ISelectedEntityRenameCommand
{
    private readonly ILogger<SelectedEntityRenameCommand> _logger;

    public SelectedEntityRenameCommand(ILogger<SelectedEntityRenameCommand> logger)
    {
        _logger = logger;
    }

    public SelectedEntityCommandResult Execute(SelectedEntityCommandTarget target)
    {
        _logger.Log($"Starting rename for entity {target.PrimaryEntity.Id}:{target.PrimaryEntity.Name}");
        return new SelectedEntityCommandResult(RenameEntityId: target.PrimaryEntity.Id);
    }
}
