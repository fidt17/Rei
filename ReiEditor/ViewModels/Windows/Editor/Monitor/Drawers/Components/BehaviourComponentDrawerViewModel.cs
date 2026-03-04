using System;
using System.Collections.ObjectModel;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.FileSystem;
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
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IEntityManagementService _entityManagementService;
    private readonly ISerializableObjectsRegistry _serializableObjectsRegistry;
    private readonly IAssetSearchService _assetSearchService;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IAssetTypeMapper _assetTypeMapper;
    private readonly IProjectAssetFocusService _projectAssetFocusService;
    private readonly ITextEditorFileOpener _textEditorFileOpener;

#pragma warning disable CS8618
    public BehaviourComponentDrawerViewModel() { }
#pragma warning restore CS8618

    public BehaviourComponentDrawerViewModel(
        GameEntity entity,
        BehaviourComponent behaviourComponent,
        IBehaviourRegistry behaviourRegistry, 
        IEntityManagementService entityManagementService,
        ISerializableObjectsRegistry serializableObjectsRegistry,
        IAssetSearchService assetSearchService,
        IAssetRegistry assetRegistry,
        IAssetTypeMapper assetTypeMapper,
        IProjectAssetFocusService projectAssetFocusService,
        ITextEditorFileOpener textEditorFileOpener)
    {
        _entity = entity;
        BehaviourComponent = behaviourComponent;
        _behaviourRegistry = behaviourRegistry;
        _entityManagementService = entityManagementService;
        _serializableObjectsRegistry = serializableObjectsRegistry;
        _assetSearchService = assetSearchService;
        _assetRegistry = assetRegistry;
        _assetTypeMapper = assetTypeMapper;
        _projectAssetFocusService = projectAssetFocusService;
        _textEditorFileOpener = textEditorFileOpener;

        if (!behaviourRegistry.TryGetById(behaviourComponent.Id, out var behaviourInfo))
        {
            throw new Exception($"Could not load behaviour {behaviourComponent.Id}");
        }
        
        Name = behaviourInfo.ObjectName;
        ContextMenu = SetupContextMenu(behaviourInfo);
        SetupProperties();
    }

    public override void Dispose()
    {
        base.Dispose();
        
        Properties.ClearAndDispose();
        ContextMenu.Dispose();
    }

    public void SwitchExpandState() => Expanded.Value = !Expanded.Value;

    private void DeleteComponent() => _entityManagementService.DeleteBehaviour(_entity, BehaviourComponent.Id);

    private ContextMenuViewModel SetupContextMenu(BehaviourAssetInfo behaviourInfo)
    {
        var contextMenu = new ContextMenuViewModel();
        contextMenu.AddOption(new ContextMenuOption("Show in project", () => ShowInProject(behaviourInfo)));
        contextMenu.AddOption(new ContextMenuOption("Edit", () => EditBehaviourSource(behaviourInfo)));
        contextMenu.AddOption(new ContextMenuOption("Delete Component", DeleteComponent));
        return contextMenu;
    }

    private void ShowInProject(BehaviourAssetInfo behaviourInfo)
    {
        _projectAssetFocusService.FocusAssetPath(behaviourInfo.Source.FullPath);
    }

    private void EditBehaviourSource(BehaviourAssetInfo behaviourInfo)
    {
        _textEditorFileOpener.Open(behaviourInfo.Source.FullPath);
    }
    
    private void SetupProperties()
    {
        Properties.ClearAndDispose();
        
        foreach (var (propertyName, propertyType) in _behaviourRegistry.Behaviours[BehaviourComponent.Id].SerializedProperties)
        {
            if (propertyType.HideInEditor) continue;
            if (!BehaviourComponent.HasProperty(propertyName))
                throw new Exception($"Behaviour does not have property with name {propertyName} of {propertyType}");
            
            var property = BehaviourComponent.GetProperty(propertyName);
            Properties.Add(PropertyViewUtils.CreatePropertyViewModel(property, _serializableObjectsRegistry, _assetSearchService, _assetRegistry, _assetTypeMapper, _projectAssetFocusService));
        }
    }
}
