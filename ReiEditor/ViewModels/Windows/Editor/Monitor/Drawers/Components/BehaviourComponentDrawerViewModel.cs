using System;
using ReiEditor.Models.Services.Assets.Behaviours;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;

public class BehaviourComponentDrawerViewModel : BaseViewModel
{
    public string Name { get; }
    
    private readonly GameEntity _entity;
    private readonly BehaviourComponent _behaviourComponent;

#pragma warning disable CS8618
    public BehaviourComponentDrawerViewModel() { }
#pragma warning restore CS8618

    public BehaviourComponentDrawerViewModel(GameEntity entity, BehaviourComponent behaviourComponent, IBehaviourComponentsService behaviourComponentsService)
    {
        _entity = entity;
        _behaviourComponent = behaviourComponent;

        var behaviourInfo = behaviourComponentsService.GetBehaviourById(behaviourComponent.Id);
        if (behaviourInfo == null) throw new Exception($"Could not load behaviour {behaviourComponent.Id}");
        Name = behaviourInfo.BehaviourName;
    }
}