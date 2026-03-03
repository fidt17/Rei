using System;
using System.Text;
using ReiEditor.Utils.Common;

namespace ReiEditor.Models.Services.Engine.Api;

public class AssetApi : IAssetApi
{
    private readonly Pool<StringBuilder> _responseBufferPool = new(() => new StringBuilder(16384), x => x.Clear());
    private readonly IEngineApi _engineApi;

    public AssetApi(IEngineApi engineApi)
    {
        _engineApi = engineApi;
    }

    private delegate bool GetAssetDataDelegate(string assetId, string assetType, StringBuilder outputBuffer, int bufferSize);
    public bool TryGetAssetData(string assetId, string assetType, out string jsonData)
    {
        jsonData = "";
        if (!_engineApi.IsEngineRunning) return false;
        if (string.IsNullOrWhiteSpace(assetId)) return false;
        if (string.IsNullOrWhiteSpace(assetType)) return false;

        try
        {
            var buffer = _responseBufferPool.Get();
            var success = _engineApi.Invoke<bool>(typeof(GetAssetDataDelegate), "GetAssetData", assetId, assetType, buffer, buffer.Capacity);
            jsonData = buffer.ToString();
            _responseBufferPool.Put(buffer);
            return success;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private delegate bool SetAssetDataDelegate(string assetId, string assetType, string jsonData);
    public bool TrySetAssetData(string assetId, string assetType, string jsonData)
    {
        if (!_engineApi.IsEngineRunning) return false;
        if (string.IsNullOrWhiteSpace(assetId)) return false;
        if (string.IsNullOrWhiteSpace(assetType)) return false;

        try
        {
            return _engineApi.Invoke<bool>(typeof(SetAssetDataDelegate), "SetAssetData", assetId, assetType, jsonData);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private delegate bool PatchAssetDataDelegate(string assetId, string assetType, string jsonPatch);
    public bool TryPatchAssetData(string assetId, string assetType, string jsonPatch)
    {
        if (!_engineApi.IsEngineRunning) return false;
        if (string.IsNullOrWhiteSpace(assetId)) return false;
        if (string.IsNullOrWhiteSpace(assetType)) return false;

        try
        {
            return _engineApi.Invoke<bool>(typeof(PatchAssetDataDelegate), "PatchAssetData", assetId, assetType, jsonPatch);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
