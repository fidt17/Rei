using ReiEditor.Models.Services.Entities;
using ReiEditor.ViewModels.Common;

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
                _entityManagementService.RenameEntity(_entity, value);
            }
        }
    }

    #endregion

    private readonly GameEntity _entity;
    private readonly IEntityManagementService _entityManagementService;

#pragma warning disable CS8618
    public EntityInfoComponentDrawerViewModel() { }
#pragma warning restore CS8618

    public EntityInfoComponentDrawerViewModel(GameEntity e, IEntityManagementService entityManagementService)
    {
        _entity = e;
        _entityManagementService = entityManagementService;

        SceneId = e.Id.ToString();
        EntityName = e.Name;
        
        _entity.NameChangedEvent += HandleEntityNameChangedEvent;
    }

    public override void Dispose()
    {
        base.Dispose();
        
        _entity.NameChangedEvent -= HandleEntityNameChangedEvent;
    }

    private void HandleEntityNameChangedEvent(GameEntity e, string name)
    {
        EntityName = name;
    }
}