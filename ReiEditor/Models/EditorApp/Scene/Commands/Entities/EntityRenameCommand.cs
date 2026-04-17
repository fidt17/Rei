using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.EditorApp.Scene.Commands.Entities;

public class EntityRenameCommand : IEntityRenameCommand
{
    private readonly IEntityManagementService _entityManagementService;
    private readonly ILogger<EntityRenameCommand> _logger;

    public EntityRenameCommand(IEntityManagementService entityManagementService, ILogger<EntityRenameCommand> logger)
    {
        _entityManagementService = entityManagementService;
        _logger = logger;
    }

    public void Execute(EntityRenameCommandTarget target)
    {
        _logger.Log($"Renaming entity {target.Entity.Id}:{target.Entity.Name} -> {target.Name}");
        _entityManagementService.RenameEntity(target.Entity, target.Name);
    }
}
