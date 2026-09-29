using ReiEditor.Mcp.Contracts;

namespace ReiEditor.Models.Services.Engine.Api;

public interface IAssetRuntimeInspectionService
{
    ReiAssetState Read(string assetId);
}
