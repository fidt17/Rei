using System;
using System.Text;
using System.Text.Json;
using ReiEditor.Mcp.Contracts;

namespace ReiEditor.Models.Services.Engine.Api;

public sealed class AssetRuntimeInspectionService : IAssetRuntimeInspectionService
{
    private delegate int GetLoadedAssetStateDelegate(string assetId, [System.Runtime.InteropServices.Out] byte[] buffer, int bufferSize);
    private const int INITIAL_BUFFER_SIZE = 16384;
    private const int MAX_BUFFER_SIZE = 16 * 1024 * 1024;
    private readonly IEngineApi _engineApi;

    public AssetRuntimeInspectionService(IEngineApi engineApi) => _engineApi = engineApi;

    public ReiAssetState Read(string assetId)
    {
        if (!_engineApi.IsEngineRunning) return new(assetId, "runtime", "engine_unavailable", null);
        var size = INITIAL_BUFFER_SIZE;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var buffer = new byte[size];
            var required = _engineApi.Invoke<int>(typeof(GetLoadedAssetStateDelegate), "GetLoadedAssetState", assetId, buffer, size);
            if (required <= 0) return new(assetId, "runtime", "read_failed", null);
            if (required > MAX_BUFFER_SIZE) return new(assetId, "runtime", "too_large", null);
            if (required > size)
            {
                size = required;
                continue;
            }
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(buffer, 0, required - 1));
            var root = document.RootElement;
            var values = root.GetProperty("values");
            return new(assetId, "runtime", root.GetProperty("status").GetString()!,
                values.ValueKind == JsonValueKind.Null ? null : values.Clone());
        }
        return new(assetId, "runtime", "read_failed", null);
    }
}
