using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
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
    private readonly List<(SerializedProperty Property, Action<object?> Handler)> _subscriptions = new();
    private CancellationTokenSource? _runtimeSyncDebounceCts;

#pragma warning disable CS8618
    public DataAssetMonitorDrawerViewModel() { }
#pragma warning restore CS8618

    public DataAssetMonitorDrawerViewModel(IAssetSelectable assetSelection, IDataAssetService dataAssetService, ISerializableObjectsRegistry serializableObjectsRegistry, IDataAssetTypeRegistry dataAssetTypeRegistry, IAssetSearchService assetSearchService, IAssetRegistry assetRegistry, IAssetTypeMapper assetTypeMapper, IBehaviourRegistry behaviourRegistry, IProjectAssetFocusService projectAssetFocusService, ISceneManagementService sceneManagementService, ISelectionService selectionService)
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

        LoadingTask = LoadState();
    }

    public override void Dispose()
    {
        if (_isDisposed) return;
        var hasPendingSync = _runtimeSyncDebounceCts != null;
        CancelRuntimeSync();
        if (hasPendingSync) SyncRuntimeImmediate();
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
            if (_asset == null)
            {
                await Dispatcher.UIThread.InvokeAsync(() => StatusText = "Failed to load DataAsset.");
                return;
            }

            var typeInfo = _dataAssetTypeRegistry.GetDataAssetType(_asset.DataAssetTypeId);
            if (typeInfo == null)
            {
                await Dispatcher.UIThread.InvokeAsync(() => StatusText = $"Unknown DataAsset type id {_asset.DataAssetTypeId}.");
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_isDisposed) return;
                TypeLabel = $"Type: {typeInfo.ObjectName} ({_asset.DataAssetTypeId})";
                var visibleProperties = PropertyDisplayUtils.GetVisibleProperties(_asset.Properties.Values, typeInfo.SerializedProperties).ToList();
                foreach (var row in PropertyDisplayUtils.CreateRows(visibleProperties, typeInfo.SerializedProperties, (property, metadata) =>
                             PropertyViewUtils.CreatePropertyViewModel(property, _serializableObjectsRegistry, _assetSearchService, _assetRegistry, _assetTypeMapper, _behaviourRegistry, _projectAssetFocusService, _sceneManagementService, _selectionService, _dataAssetTypeRegistry, metadata)))
                {
                    Properties.Add(row);
                }
                foreach (var property in visibleProperties)
                {
                    Action<object?> handler = _ => ScheduleRuntimeSync();
                    property.ValueChangedEvent += handler;
                    _subscriptions.Add((property, handler));
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

    private void ScheduleRuntimeSync()
    {
        if (_isDisposed) return;
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
        StatusText = _dataAssetService.TrySyncRuntime(_asset) ? "" :
            "Runtime not synchronized. Live changes require a loaded asset; RendererSettings affects cameras referencing this profile.";
    }

    private void CancelRuntimeSync()
    {
        _runtimeSyncDebounceCts?.Cancel();
        _runtimeSyncDebounceCts?.Dispose();
        _runtimeSyncDebounceCts = null;
    }
}
