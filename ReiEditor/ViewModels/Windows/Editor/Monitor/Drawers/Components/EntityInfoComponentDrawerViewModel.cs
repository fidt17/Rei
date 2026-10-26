using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.EditorApp.Scene.Commands.Entities;
using ReiEditor.ViewModels.Common;
using ReiEditor.Models.Services.Logging.Loggers;
using System;
using System.Threading.Tasks;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;

public class EntityInfoComponentDrawerViewModel : BaseViewModel
{
    public string SceneId { get; } = "0";
    
    #region EntityName

    private string _entityName = "Name";
    public string EntityName
    {
        get => _entityName;
        set
        {
            if (SetField(ref _entityName, value))
            {
                _ = RenameAsync(value);
            }
        }
    }

    #endregion

    private readonly GameEntity _entity;
    private readonly IEntityRenameCommand _entityRenameCommand;
    private readonly ILogger<EntityInfoComponentDrawerViewModel> _logger;

#pragma warning disable CS8618
    public EntityInfoComponentDrawerViewModel() { }
#pragma warning restore CS8618

    public EntityInfoComponentDrawerViewModel(GameEntity e, IEntityRenameCommand entityRenameCommand, ILogger<EntityInfoComponentDrawerViewModel> logger)
    {
        _entity = e;
        _entityRenameCommand = entityRenameCommand;
        _logger = logger;

        SceneId = e.Id.ToString();
        _entityName = e.Name;
        
        _entity.NameChangedEvent += HandleEntityNameChangedEvent;
    }

    public override void Dispose()
    {
        base.Dispose();
        
        _entity.NameChangedEvent -= HandleEntityNameChangedEvent;
    }

    private async Task RenameAsync(string name)
    {
        try { await _entityRenameCommand.ExecuteAsync(new EntityRenameCommandTarget(_entity, name)); }
        catch (Exception error) { _logger.LogException(error); }
        finally { SetField(ref _entityName, _entity.Name, nameof(EntityName)); }
    }

    private void HandleEntityNameChangedEvent(GameEntity e, string name)
    {
        SetField(ref _entityName, name, nameof(EntityName));
    }
}
