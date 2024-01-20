using System;
using System.Threading.Tasks;
using ReiEditor.Models.Resources;

namespace ReiEditor.Models.Services.Assets;

public interface IAssetCreator
{
    event Action<AssetInfo, Asset>? AssetCreatedEvent;

    string AllocateAssetId();
    Task<bool> Create(Asset asset, string projectPath);
    Task<bool> Create(Asset asset, string id, string projectPath);
    Task<ObjectFile<AssetMeta>> CreateMetaFile(AssetMeta meta, string fullPath);
}