using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;

public class BehaviourComponentDrawerViewModel : BaseViewModel
{
    private readonly GameEntity _entity;
    private readonly BehaviourComponent _behaviourComponent;

#pragma warning disable CS8618
    public BehaviourComponentDrawerViewModel() { }
#pragma warning restore CS8618

    public BehaviourComponentDrawerViewModel(GameEntity entity, BehaviourComponent behaviourComponent)
    {
        _entity = entity;
        _behaviourComponent = behaviourComponent;
    }
}