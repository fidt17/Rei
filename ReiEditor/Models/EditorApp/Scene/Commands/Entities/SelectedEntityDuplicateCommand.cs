using System.Collections.Generic;
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
        var duplicatedEntityIds = new List<int>();

        foreach (var entity in target.Entities)
        {
            _logger.Log($"Duplicating entity {entity.Id}:{entity.Name}");
            var duplicatedEntityId = _entityManagementService.InstantiateEntity(entity);
            if (duplicatedEntityId.HasValue)
            {
                duplicatedEntityIds.Add(duplicatedEntityId.Value);
            }
        }

        return new SelectedEntityCommandResult(duplicatedEntityIds);
    }
}
