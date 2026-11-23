using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public class SelectedEntityDuplicateCommand : ISelectedEntityDuplicateCommand
{
    private readonly IEntityManagementService _entityManagementService;
    private readonly ILogger<SelectedEntityDuplicateCommand> _logger;

    public SelectedEntityDuplicateCommand(
        IEntityManagementService entityManagementService,
        ILogger<SelectedEntityDuplicateCommand> logger)
    {
        _entityManagementService = entityManagementService;
        _logger = logger;
    }

    public SelectedEntityCommandResult Execute(SelectedEntityCommandTarget target)
    {
        foreach (var entity in target.Entities)
            _logger.Log($"Duplicating entity {entity.Id}:{entity.Name}");
        if (target.Entities.Count != 1) return new SelectedEntityCommandResult(_entityManagementService.InstantiateEntities(target.Entities));

        var id = _entityManagementService.InstantiateEntity(target.Entities[0], recordHistory: true);
        return new SelectedEntityCommandResult(id.HasValue ? [id.Value] : []);
    }
}
