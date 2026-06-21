using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Utils.Common;
using ReiEditor.Utils.Extensions;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Utils;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;

public class CustomPropertyViewModel : BaseViewModel
{
    public PropertyNameViewModel PropertyName { get; }
    
    public ObservableCollection<BaseViewModel> Value { get; } = new();
    public ObservableField<bool> Expanded { get; } = new(false);
    
    private readonly SerializedProperty _property;
    private readonly ISerializableObjectsRegistry _serializableObjectsRegistry;
    private readonly IAssetSearchService _assetSearchService;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IAssetTypeMapper _assetTypeMapper;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IProjectAssetFocusService _projectAssetFocusService;
    private readonly ISceneManagementService _sceneManagementService;
    private readonly ISelectionService _selectionService;

#pragma warning disable CS8618
    public CustomPropertyViewModel() { }
#pragma warning restore CS8618

    public CustomPropertyViewModel(
        SerializedProperty property,
        ISerializableObjectsRegistry serializableObjectsRegistry,
        IAssetSearchService assetSearchService,
        IAssetRegistry assetRegistry,
        IAssetTypeMapper assetTypeMapper,
        IBehaviourRegistry behaviourRegistry,
        IProjectAssetFocusService projectAssetFocusService,
        ISceneManagementService sceneManagementService,
        ISelectionService selectionService)
    {
        if (property.Type != SerializedTypeEnum.Custom) throw new Exception($"Invalid property type. Expected {SerializedTypeEnum.Custom}. Actual {property.Type}");
        
        _property = property;
        _serializableObjectsRegistry = serializableObjectsRegistry;
        _assetSearchService = assetSearchService;
        _assetRegistry = assetRegistry;
        _assetTypeMapper = assetTypeMapper;
        _behaviourRegistry = behaviourRegistry;
        _projectAssetFocusService = projectAssetFocusService;
        _sceneManagementService = sceneManagementService;
        _selectionService = selectionService;

        PropertyName = new(property);
        _property.ValueChangedEvent += HandlePropertyValueChangedEvent;
        
        HandlePropertyValueChangedEvent(_property.Value);
    }

    public override void Dispose()
    {
        base.Dispose();
        
        _property.ValueChangedEvent -= HandlePropertyValueChangedEvent;
        Value.ClearAndDispose();
    }
    
    public void SwitchExpandState() => Expanded.Value = !Expanded.Value;

    private void HandlePropertyValueChangedEvent(object? value)
    {
        Dispatcher.UIThread.Execute(() => HandlePropertyValueChangedEventOnUiThread(value));
    }

    private void HandlePropertyValueChangedEventOnUiThread(object? value)
    {
        if (value is null)
        {
            Value.ClearAndDispose();
            return;
        }
        
        if (value is Dictionary<string, SerializedProperty> subProperties)
        {
            if (HasSameProperties(subProperties)) return;

            Value.ClearAndDispose();
            
            foreach (var subProperty in subProperties)
            {
                Value.Add(PropertyViewUtils.CreatePropertyViewModel(subProperty.Value, _serializableObjectsRegistry, _assetSearchService, _assetRegistry, _assetTypeMapper, _behaviourRegistry, _projectAssetFocusService, _sceneManagementService, _selectionService));
            }
        }
        else
        {
            throw new Exception($"Not supported value type: {value.GetType()} {value}");
        }
    }

    private bool HasSameProperties(Dictionary<string, SerializedProperty> subProperties)
    {
        var currentProperties = Value.ToArray();

        if (currentProperties.Length != subProperties.Count) return false;

        var propertyNames = currentProperties
            .Select(GetPropertyName)
            .ToList();

        if (propertyNames.Count != subProperties.Count || propertyNames.Any(propertyName => propertyName == null)) return false;

        var index = 0;
        foreach (var (_, property) in subProperties)
        {
            if (index >= propertyNames.Count || propertyNames[index] != property.Name)
            {
                return false;
            }

            index++;
        }

        return true;
    }

    private static string? GetPropertyName(BaseViewModel viewModel)
    {
        return viewModel switch
        {
            BasePropertyViewModel<string> propertyViewModel => propertyViewModel.PropertyName.Value,
            BasePropertyViewModel<float> propertyViewModel => propertyViewModel.PropertyName.Value,
            BasePropertyViewModel<int> propertyViewModel => propertyViewModel.PropertyName.Value,
            BasePropertyViewModel<bool> propertyViewModel => propertyViewModel.PropertyName.Value,
            BasePropertyViewModel<double> propertyViewModel => propertyViewModel.PropertyName.Value,
            BaseCustomPropertyViewModel propertyViewModel => propertyViewModel.PropertyName.Value,
            CustomPropertyViewModel propertyViewModel => propertyViewModel.PropertyName.Value,
            CollectionPropertyViewModel propertyViewModel => propertyViewModel.PropertyName.Value,
            null => null,
            _ => null
        };
    }
}
