using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Assets;

namespace ReiEditor.Models.Services.Assets.Sync;

public class AssetRuntimeSyncService : IAssetRuntimeSyncService
{
    private readonly IAssetApi _assetApi;
    private readonly IAssetRegistry _assetRegistry;
    private readonly ILogger<AssetRuntimeSyncService> _logger;
    private readonly IAssetRuntimeInspectionService _runtimeInspection;

    public AssetRuntimeSyncService(
        IAssetApi assetApi,
        IAssetRegistry assetRegistry,
        ILogger<AssetRuntimeSyncService> logger,
        IAssetRuntimeInspectionService runtimeInspection)
    {
        _assetApi = assetApi;
        _assetRegistry = assetRegistry;
        _logger = logger;
        _runtimeInspection = runtimeInspection;
    }

    public bool TryGetAssetData(string assetId, out string jsonData)
    {
        jsonData = "";
        if (string.IsNullOrWhiteSpace(assetId)) return false;
        if (!RuntimeAssetTypeResolver.TryResolveAssetType(_assetRegistry, assetId, out var assetType))
        {
            _logger.LogWarning($"Asset runtime sync get failed. Unsupported asset type. assetId={assetId}");
            return false;
        }

        var success = _assetApi.TryGetAssetData(assetId, assetType, out jsonData);
        if (!success)
        {
            _logger.LogWarning($"Asset runtime sync get failed. assetId={assetId}");
        }

        return success;
    }

    public bool TrySetAssetData(string assetId, string jsonData)
    {
        if (string.IsNullOrWhiteSpace(assetId)) return false;
        if (!RuntimeAssetTypeResolver.TryResolveAssetType(_assetRegistry, assetId, out var assetType))
        {
            _logger.LogWarning($"Asset runtime sync set failed. Unsupported asset type. assetId={assetId}");
            return false;
        }

        var success = _assetApi.TrySetAssetData(assetId, assetType, jsonData);
        if (!success)
        {
            var state = _runtimeInspection.Read(assetId);
            if (state.Status is "unloaded" or "engine_unavailable")
                _logger.Log($"Asset runtime sync skipped. status={state.Status}, assetId={assetId}. Editor value retained; live changes require a loaded asset.");
            else
                _logger.LogWarning($"Asset runtime sync set failed. status={state.Status}, assetId={assetId}");
        }

        return success;
    }

    public bool TryPatchAssetData(string assetId, string jsonPatch)
    {
        if (string.IsNullOrWhiteSpace(assetId)) return false;
        if (!RuntimeAssetTypeResolver.TryResolveAssetType(_assetRegistry, assetId, out var assetType))
        {
            _logger.LogWarning($"Asset runtime sync patch failed. Unsupported asset type. assetId={assetId}");
            return false;
        }

        var success = _assetApi.TryPatchAssetData(assetId, assetType, jsonPatch);
        if (!success)
        {
            _logger.LogWarning($"Asset runtime sync patch failed. assetId={assetId}");
        }

        return success;
    }
}
