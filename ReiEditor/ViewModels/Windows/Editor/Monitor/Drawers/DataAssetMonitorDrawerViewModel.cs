using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.Project.Commands.Assets;
using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.DataAssets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Components;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Utils;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

public sealed class DataAssetMonitorDrawerViewModel : BaseMonitorDrawer
{
    private const int RUNTIME_SYNC_DEBOUNCE_DELAY_MS = 40;

    public string AssetName { get; }
    public string AssetId { get; }
    public string AssetIdLabel { get; }
    public ObservableCollection<BaseViewModel> Properties { get; } = new();
    internal Task LoadingTask { get; } = Task.CompletedTask;

    private string _typeLabel = "";
    public string TypeLabel
    {
        get => _typeLabel;
        private set => SetField(ref _typeLabel, value);
    }

    private string _statusText = "Loading DataAsset...";
    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    private bool _isLoaded;
    public bool IsLoaded
    {
        get => _isLoaded;
        private set => SetField(ref _isLoaded, value);
    }

    private bool _isDisposed;
    private DataAsset? _asset;
    private DataAssetTypeInfo? _boundType;
    private int _boundPropertyCount;
    private readonly IDataAssetService _dataAssetService;
    private readonly ISerializableObjectsRegistry _serializableObjectsRegistry;
    private readonly IDataAssetTypeRegistry _dataAssetTypeRegistry;
    private readonly IAssetSearchService _assetSearchService;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IAssetTypeMapper _assetTypeMapper;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IProjectAssetFocusService _projectAssetFocusService;
    private readonly ISceneManagementService _sceneManagementService;
    private readonly ISelectionService _selectionService;
    private readonly SetDataAssetPropertyCommand? _setProperty;
    private readonly List<(SerializedProperty Property, Action<object?> Handler)> _subscriptions = new();
    private CancellationTokenSource? _runtimeSyncDebounceCts;

#pragma warning disable CS8618
    public DataAssetMonitorDrawerViewModel() { }
#pragma warning restore CS8618

    public DataAssetMonitorDrawerViewModel(IAssetSelectable assetSelection, IDataAssetService dataAssetService, ISerializableObjectsRegistry serializableObjectsRegistry, IDataAssetTypeRegistry dataAssetTypeRegistry, IAssetSearchService assetSearchService, IAssetRegistry assetRegistry, IAssetTypeMapper assetTypeMapper, IBehaviourRegistry behaviourRegistry, IProjectAssetFocusService projectAssetFocusService, ISceneManagementService sceneManagementService, ISelectionService selectionService, SetDataAssetPropertyCommand? setProperty = null)
    {
        AssetName = assetSelection.AssetName;
        AssetId = assetSelection.AssetId;
        AssetIdLabel = string.IsNullOrWhiteSpace(AssetId) ? "ID: <missing>" : $"ID: {AssetId}";
        _dataAssetService = dataAssetService;
        _serializableObjectsRegistry = serializableObjectsRegistry;
        _dataAssetTypeRegistry = dataAssetTypeRegistry;
        _assetSearchService = assetSearchService;
        _assetRegistry = assetRegistry;
        _assetTypeMapper = assetTypeMapper;
        _behaviourRegistry = behaviourRegistry;
        _projectAssetFocusService = projectAssetFocusService;
        _sceneManagementService = sceneManagementService;
        _selectionService = selectionService;
        _setProperty = setProperty;

        LoadingTask = LoadState();
    }

    internal bool HasCurrentBindings(Asset asset)
    {
        if (!IsLoaded || asset is not DataAsset dataAsset || !ReferenceEquals(_asset, dataAsset)
            || dataAsset.Properties.Count != _boundPropertyCount || _boundType is not { } bound
            || _dataAssetTypeRegistry.GetDataAssetType(dataAsset.DataAssetTypeId) is not { } currentType) return false;

        return bound.TypeId == currentType.TypeId
            && bound.SerializableObject.Source.Object == currentType.SerializableObject.Source.Object
            && _subscriptions.All(binding => dataAsset.Properties.TryGetValue(binding.Property.Name, out var current)
                && ReferenceEquals(current, binding.Property));
    }

    public override void Dispose()
    {
        if (_isDisposed) return;
        FlushPendingRuntimeSync();
        _isDisposed = true;
        base.Dispose();
        foreach (var (property, handler) in _subscriptions)
        {
            property.ValueChangedEvent -= handler;
        }
        _subscriptions.Clear();
        Properties.ClearAndDispose();
    }

    private async Task LoadState()
    {
        try
        {
            var asset = await _dataAssetService.Load(AssetId);
            if (_isDisposed) return;
            _asset = asset;
            if (asset == null)
            {
                await Dispatcher.UIThread.InvokeAsync(() => StatusText = "Failed to load DataAsset.");
                return;
            }

            var typeInfo = _dataAssetTypeRegistry.GetDataAssetType(asset.DataAssetTypeId);
            if (typeInfo == null)
            {
                await Dispatcher.UIThread.InvokeAsync(() => StatusText = $"Unknown DataAsset type id {asset.DataAssetTypeId}.");
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_isDisposed) return;
                _boundType = typeInfo;
                _boundPropertyCount = asset.Properties.Count;
                TypeLabel = $"Type: {typeInfo.ObjectName} ({asset.DataAssetTypeId})";
                var visibleProperties = PropertyDisplayUtils.GetVisibleProperties(asset.Properties.Values, typeInfo.SerializedProperties);
                foreach (var row in PropertyDisplayUtils.CreateRows(visibleProperties, typeInfo.SerializedProperties, (property, metadata) =>
                             CreatePropertyRow(asset, property, metadata)))
                {
                    Properties.Add(row);
                }
                StatusText = Properties.Count == 0 ? "DataAsset has no editable properties." : "";
                IsLoaded = true;
            });
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!_isDisposed) StatusText = $"Failed to load DataAsset. {exception.Message}";
            });
        }
    }

    private BaseViewModel CreatePropertyRow(DataAsset asset, SerializedProperty property, SerializableObjectInfo.SerializedPropertyData? metadata)
    {
        var recordEdit = _setProperty != null && SetDataAssetPropertyCommand.Supports(property);
        var recordNestedEdit = _setProperty != null && SetDataAssetPropertyCommand.SupportsNestedFields(property);
        var previewRange = recordEdit && metadata?.Range != null;
        var previewColor = recordEdit && property.SourceType == "Color";
        var view = PropertyViewUtils.CreatePropertyViewModel(property, _serializableObjectsRegistry, _assetSearchService, _assetRegistry,
            _assetTypeMapper, _behaviourRegistry, _projectAssetFocusService, _sceneManagementService, _selectionService, _dataAssetTypeRegistry,
            metadata, recordEdit || recordNestedEdit ? CommitProperty : null,
            recordColorDrag: previewColor ? before => RecordDrag(asset, property, before) : null,
            recordRangeDrag: previewRange ? (field, before) => RecordDrag(asset, field, before) : null);
        Action<object?> handler = _ =>
        {
            if (_setProperty?.IsApplying(property) != true) ScheduleRuntimeSync(keepPending: previewRange || previewColor);
        };
        property.ValueChangedEvent += handler;
        _subscriptions.Add((property, handler));
        return view;
    }

    private async void CommitProperty(SerializedProperty property, object value)
    {
        if (_isDisposed || _asset == null || _setProperty == null) return;
        try
        {
            var result = await _setProperty.SetAsync(_asset, property, value);
            if (!_isDisposed && result != null) ShowRuntimeSyncStatus(result.RuntimeSynced);
        }
        catch (Exception exception)
        {
            if (!_isDisposed) StatusText = $"Failed to edit DataAsset. {exception.Message}";
        }
    }

    private void RecordDrag(DataAsset asset, SerializedProperty property, object before)
    {
        _setProperty?.RecordApplied(asset, property, before);
        FlushPendingRuntimeSync();
    }

    private void ScheduleRuntimeSync(bool keepPending)
    {
        if (_isDisposed || (keepPending && _runtimeSyncDebounceCts != null)) return;
        CancelRuntimeSync();
        _runtimeSyncDebounceCts = new CancellationTokenSource();
        var token = _runtimeSyncDebounceCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(RUNTIME_SYNC_DEBOUNCE_DELAY_MS, token);
                if (token.IsCancellationRequested) return;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_isDisposed || token.IsCancellationRequested) return;
                    CancelRuntimeSync();
                    SyncRuntimeImmediate();
                });
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    private void SyncRuntimeImmediate()
    {
        if (_isDisposed || _asset == null || string.IsNullOrWhiteSpace(AssetId)) return;
        ShowRuntimeSyncStatus(_dataAssetService.TrySyncRuntime(_asset));
    }

    private void FlushPendingRuntimeSync()
    {
        if (_runtimeSyncDebounceCts == null) return;
        CancelRuntimeSync();
        SyncRuntimeImmediate();
    }

    private void ShowRuntimeSyncStatus(bool synced)
    {
        StatusText = synced ? "" :
            "Runtime not synchronized. Live changes require a loaded asset; RendererSettings affects cameras referencing this profile.";
    }

    private void CancelRuntimeSync()
    {
        _runtimeSyncDebounceCts?.Cancel();
        _runtimeSyncDebounceCts?.Dispose();
        _runtimeSyncDebounceCts = null;
    }
}
