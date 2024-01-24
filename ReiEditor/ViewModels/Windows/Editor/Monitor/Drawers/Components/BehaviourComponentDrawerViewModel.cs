using System;
using ReiEditor.Models.Services.Assets.Behaviours;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Utils.Common;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Controls;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;

public class BehaviourComponentDrawerViewModel : BaseViewModel
{
    public string Name { get; } = "Behaviour Name";
    
    public ContextMenuViewModel ContextMenu { get; }
    public BehaviourComponent BehaviourComponent { get; }
    public ObservableField<bool> Expanded { get; } = new(true);
    
    private readonly GameEntity _entity;
    private readonly IBehaviourComponentsService _behaviourComponentsService;

#pragma warning disable CS8618
    public BehaviourComponentDrawerViewModel() { }
#pragma warning restore CS8618

    public BehaviourComponentDrawerViewModel(GameEntity entity, BehaviourComponent behaviourComponent, IBehaviourComponentsService behaviourComponentsService)
    {
        _entity = entity;
        BehaviourComponent = behaviourComponent;
        _behaviourComponentsService = behaviourComponentsService;

        var behaviourInfo = behaviourComponentsService.GetBehaviourById(behaviourComponent.Id);
        if (behaviourInfo == null) throw new Exception($"Could not load behaviour {behaviourComponent.Id}");
        Name = behaviourInfo.BehaviourName;

        ContextMenu = new ContextMenuViewModel();
        ContextMenu.AddOption(new ContextMenuViewModel.ContextMenuOption("Delete Component", DeleteComponent));
    }

    public void SwitchExpandState() => Expanded.Value = !Expanded.Value;

    private void DeleteComponent() => _behaviourComponentsService.DeleteComponent(_entity, BehaviourComponent);
}