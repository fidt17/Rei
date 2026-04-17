using ReiEditor.Models.Services.Assets;

namespace ReiEditor.Models.Services.Scenes;

public sealed record SceneAssetDropTarget(string AssetPath, AssetType AssetType, string AssetId, string EntityName);
