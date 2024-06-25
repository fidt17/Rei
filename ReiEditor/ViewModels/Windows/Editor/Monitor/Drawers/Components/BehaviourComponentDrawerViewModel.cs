using System;
using System.Collections.ObjectModel;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Utils.Common;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Controls;
using ReiEditor.ViewModels.Utils;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;

public class BehaviourComponentDrawerViewModel : BaseViewModel
{
    public string Name { get; } = "Behaviour Name";
    
    public ContextMenuViewModel ContextMenu { get; }
    public BehaviourComponent BehaviourComponent { get; }
    public ObservableField<bool> Expanded { get; } = new(true);
    public ObservableCollection<BaseViewModel> Properties { get; } = new();

    private readonly GameEntity _entity;
    private readonly IBehaviourComponentsService _behaviourComponentsService;
    private readonly IBehaviourRegistry _behaviourRegistry;

#pragma warning disable CS8618
    public BehaviourComponentDrawerViewModel() { }
#pragma warning restore CS8618

    public BehaviourComponentDrawerViewModel(GameEntity entity, BehaviourComponent behaviourComponent, IBehaviourComponentsService behaviourComponentsService, IBehaviourRegistry behaviourRegistry)
    {
        _entity = entity;
        BehaviourComponent = behaviourComponent;
        _behaviourComponentsService = behaviourComponentsService;
        _behaviourRegistry = behaviourRegistry;

        if (!behaviourRegistry.TryGetById(behaviourComponent.Id, out var behaviourInfo))
        {
            throw new Exception($"Could not load behaviour {behaviourComponent.Id}");
        }
        
        Name = behaviourInfo.ObjectName;
        ContextMenu = SetupContextMenu();
        SetupProperties();
    }

    public override void Dispose()
    {
        base.Dispose();
        
        Properties.ClearAndDispose();
        ContextMenu.Dispose();
    }

    public void SwitchExpandState() => Expanded.Value = !Expanded.Value;

    private void DeleteComponent() => _behaviourComponentsService.DeleteComponent(_entity, BehaviourComponent);

    private ContextMenuViewModel SetupContextMenu()
    {
        var contextMenu = new ContextMenuViewModel();
        contextMenu.AddOption(new ContextMenuViewModel.ContextMenuOption("Delete Component", DeleteComponent));
        return contextMenu;
    }
    
    private void SetupProperties()
    {
        Properties.ClearAndDispose();
        
        foreach (var (propertyName, propertyType) in _behaviourRegistry.Behaviours[BehaviourComponent.Id].SerializedProperties)
        {
            if (!BehaviourComponent.HasProperty(propertyName))
                throw new Exception($"Behaviour does not have property with name {propertyName} of {propertyType}");
            
            var property = BehaviourComponent.GetProperty(propertyName);
            Properties.Add(PropertyViewUtils.CreatePropertyViewModel(property));
        }
    }
}