using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public class SelectedEntityDeleteCommand : ISelectedEntityDeleteCommand
{
    private readonly IEntityManagementService _entityManagementService;
    private readonly ILogger<SelectedEntityDeleteCommand> _logger;

    public SelectedEntityDeleteCommand(
        IEntityManagementService entityManagementService,
        ILogger<SelectedEntityDeleteCommand> logger)
    {
        _entityManagementService = entityManagementService;
        _logger = logger;
    }

    public SelectedEntityCommandResult Execute(SelectedEntityCommandTarget target)
    {
        foreach (var entity in target.Entities)
            _logger.Log($"Deleting entity {entity.Id}:{entity.Name}");
        if (target.Entities.Count == 1) _entityManagementService.DestroyEntity(target.Entities[0], recordHistory: true);
        else _entityManagementService.DestroyEntities(target.Entities);

        return new SelectedEntityCommandResult();
    }
}
