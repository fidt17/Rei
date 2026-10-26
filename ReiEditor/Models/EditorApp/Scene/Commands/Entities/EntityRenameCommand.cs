using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Logging.Loggers;
using System.Threading.Tasks;

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

    public Task<EntityRenameCommandResult> ExecuteAsync(EntityRenameCommandTarget target)
    {
        _logger.Log($"Renaming entity {target.Entity.Id}:{target.Entity.Name} -> {target.Name}");
        var beforeName = target.Entity.Name;
        _entityManagementService.RenameEntity(target.Entity, target.Name);
        return Task.FromResult(new EntityRenameCommandResult(beforeName, target.Entity.Name));
    }
}
