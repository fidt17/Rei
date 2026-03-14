using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.ViewModels.Controls.Assets;
using ReactiveUI;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

public class ComponentRefPropertyViewModel : BaseCustomPropertyViewModel
{
    public AssetPickerViewModel? Picker
    {
        get => _picker;
        private set => this.RaiseAndSetIfChanged(ref _picker, value);
    }

    private readonly string _requiredComponentName;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly ISceneManagementService _sceneManagementService;
    private readonly ISelectionService _selectionService;

    private Scene? _activeScene;
    private AssetPickerViewModel? _picker;

#pragma warning disable CS8618
    public ComponentRefPropertyViewModel() { }
#pragma warning restore CS8618

    public ComponentRefPropertyViewModel(
        SerializedProperty property,
        IAssetRegistry assetRegistry,
        IBehaviourRegistry behaviourRegistry,
        ISceneManagementService sceneManagementService,
        ISelectionService selectionService) : base(property)
    {
        _requiredComponentName = property.TemplateTypeName ?? "Unknown";
        _assetRegistry = assetRegistry;
        _behaviourRegistry = behaviourRegistry;
        _sceneManagementService = sceneManagementService;
        _selectionService = selectionService;

        _sceneManagementService.CurrentScene.Subscribe(HandleCurrentSceneChanged);
        RebuildPicker();
    }

    public override void Dispose()
    {
        base.Dispose();

        if (_activeScene != null)
        {
            _activeScene.HierarchyRebuiltEvent -= HandleHierarchyRebuiltEvent;
        }

        _sceneManagementService.CurrentScene.Unsubscribe(HandleCurrentSceneChanged);
        Picker?.Dispose();
    }

    protected override void HandlePropertyValueChangedEvent(object? value)
    {
        Picker?.SyncSelectedAsset(GetSceneEntityIdString());
    }

    private void HandleCurrentSceneChanged(Scene? scene)
    {
        if (_activeScene != null)
        {
            _activeScene.HierarchyRebuiltEvent -= HandleHierarchyRebuiltEvent;
        }

        _activeScene = scene;

        if (_activeScene != null)
        {
            _activeScene.HierarchyRebuiltEvent += HandleHierarchyRebuiltEvent;
        }

        RebuildPicker();
    }

    private void HandleHierarchyRebuiltEvent()
    {
        RebuildPicker();
    }

    private void RebuildPicker()
    {
        var previousPicker = Picker;
        var entries = GetSelectableEntries();

        var picker = new AssetPickerViewModel(
            _assetRegistry,
            entries,
            OnSelectedEntityChanged,
            missingEntryStateFactory: BuildMissingState,
            missingAssetName: BuildMissingComponentMessage());
        picker.AssetActivatedEvent += HandlePickerActivatedEvent;
        picker.SyncSelectedAsset(GetSceneEntityIdString());

        Picker = picker;

        if (previousPicker != null)
        {
            previousPicker.AssetActivatedEvent -= HandlePickerActivatedEvent;
            previousPicker.Dispose();
        }
    }

    private IEnumerable<AssetPickerViewModel.Entry> GetSelectableEntries()
    {
        if (_activeScene == null) return Array.Empty<AssetPickerViewModel.Entry>();

        return _activeScene.Entities
            .Where(HasRequiredComponent)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id)
            .Select(CreateEntry)
            .ToArray();
    }

    private bool HasRequiredComponent(GameEntity entity)
    {
        var requiredComponentId = _behaviourRegistry.GetIdByName(_requiredComponentName);
        if (requiredComponentId == null) return false;

        return entity.HasComponent(requiredComponentId.Value);
    }

    private AssetPickerViewModel.Entry CreateEntry(GameEntity entity)
    {
        var sceneEntityId = entity.Id.ToString(CultureInfo.InvariantCulture);
        return new AssetPickerViewModel.Entry($"{entity.Name} ({entity.Id})", sceneEntityId, sceneEntityId);
    }

    private void HandlePickerActivatedEvent()
    {
        var sceneEntityId = GetSceneEntityId();
        if (sceneEntityId == 0 || _activeScene == null) return;

        var entity = _activeScene.Entities.FirstOrDefault(x => x.Id == sceneEntityId);
        if (entity == null || !HasRequiredComponent(entity)) return;

        _selectionService.Select(entity);
    }

    private void OnSelectedEntityChanged(string? sceneEntityId, string? _)
    {
        var nestedProperty = GetNestedProperty("SceneEntityId");
        if (nestedProperty == null) return;

        nestedProperty.Value = ParseSceneEntityId(sceneEntityId);
    }

    private (string Name, bool IsMissing) BuildMissingState(string selectedId)
    {
        var sceneEntityId = ParseSceneEntityId(selectedId);
        if (sceneEntityId == 0)
        {
            return (AssetPickerViewModel.EmptyAssetName, false);
        }

        var entity = _activeScene?.Entities.FirstOrDefault(x => x.Id == sceneEntityId);
        if (entity == null)
        {
            return ($"missing entity ({sceneEntityId})", true);
        }

        return HasRequiredComponent(entity)
            ? ($"{entity.Name} ({entity.Id})", false)
            : (BuildMissingComponentMessage(), true);
    }

    private string BuildMissingComponentMessage()
    {
        return $"missing component {_requiredComponentName}";
    }

    private string GetSceneEntityIdString()
    {
        var sceneEntityId = GetSceneEntityId();
        return sceneEntityId == 0 ? string.Empty : sceneEntityId.ToString(CultureInfo.InvariantCulture);
    }

    private int GetSceneEntityId()
    {
        var nestedProperty = GetNestedProperty("SceneEntityId");
        if (nestedProperty == null) return 0;

        return ParseSceneEntityId(nestedProperty.Value);
    }

    private static int ParseSceneEntityId(object? value)
    {
        if (value == null) return 0;
        if (value is JToken token) value = token.ToObject<object?>();

        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            string stringValue when int.TryParse(stringValue, out var parsedValue) => parsedValue,
            _ => int.TryParse(value?.ToString(), out var fallbackValue) ? fallbackValue : 0
        };
    }
}
