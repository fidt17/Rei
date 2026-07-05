using System.Collections.Generic;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Engine.Playmode;
using ReiEditor.Models.Services.RectTransform;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.RectTransform;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Components;

public class RectTransformCustomPropertiesProvider : IRectTransformCustomPropertiesProvider
{
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly ISerializableObjectsRegistry _serializableObjectsRegistry;
    private readonly IAssetSearchService _assetSearchService;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IAssetTypeMapper _assetTypeMapper;
    private readonly IProjectAssetFocusService _projectAssetFocusService;
    private readonly ISceneManagementService _sceneManagementService;
    private readonly ISelectionService _selectionService;
    private readonly IEngineRunner _engineRunner;
    private readonly IEntityApi _entityApi;
    private readonly IRectTransformLayoutService _rectTransformLayoutService;

    public RectTransformCustomPropertiesProvider(
        IBehaviourRegistry behaviourRegistry,
        ISerializableObjectsRegistry serializableObjectsRegistry,
        IAssetSearchService assetSearchService,
        IAssetRegistry assetRegistry,
        IAssetTypeMapper assetTypeMapper,
        IProjectAssetFocusService projectAssetFocusService,
        ISceneManagementService sceneManagementService,
        ISelectionService selectionService,
        IEngineRunner engineRunner,
        IEntityApi entityApi,
        IRectTransformLayoutService rectTransformLayoutService)
    {
        _behaviourRegistry = behaviourRegistry;
        _serializableObjectsRegistry = serializableObjectsRegistry;
        _assetSearchService = assetSearchService;
        _assetRegistry = assetRegistry;
        _assetTypeMapper = assetTypeMapper;
        _projectAssetFocusService = projectAssetFocusService;
        _sceneManagementService = sceneManagementService;
        _selectionService = selectionService;
        _engineRunner = engineRunner;
        _entityApi = entityApi;
        _rectTransformLayoutService = rectTransformLayoutService;
    }

    public IEnumerable<BaseViewModel> CreateProperties(GameEntity entity, BehaviourComponent component)
    {
        if (!IsRectTransform(component)) yield break;

        yield return new RectTransformPropertyViewModel(
            entity,
            component,
            _engineRunner,
            _entityApi,
            _rectTransformLayoutService);

        var transformId = _behaviourRegistry.GetIdByName(EngineBehavioursConstants.TRANSFORM);
        var transform = entity.GetBehaviour(transformId);
        if (transform == null) yield break;

        foreach (var propertyName in new[] { EngineBehavioursConstants.TRANSFORM_ROTATION, EngineBehavioursConstants.TRANSFORM_SCALE })
        {
            if (!transform.HasProperty(propertyName)) continue;

            yield return CreatePropertyViewModel(transform.GetProperty(propertyName));
        }
    }

    public bool OwnsSerializedProperty(BehaviourComponent component, string propertyName)
    {
        return IsRectTransform(component) && RectTransformPropertyViewModel.OwnsProperty(propertyName);
    }

    private BaseViewModel CreatePropertyViewModel(SerializedProperty property)
    {
        return PropertyViewUtils.CreatePropertyViewModel(
            property,
            _serializableObjectsRegistry,
            _assetSearchService,
            _assetRegistry,
            _assetTypeMapper,
            _behaviourRegistry,
            _projectAssetFocusService,
            _sceneManagementService,
            _selectionService);
    }

    private bool IsRectTransform(BehaviourComponent component)
    {
        if (!_behaviourRegistry.TryGetById(component.Id, out var behaviourInfo)) return false;
        return behaviourInfo.ObjectName == EngineBehavioursConstants.RECT_TRANSFORM;
    }
}
