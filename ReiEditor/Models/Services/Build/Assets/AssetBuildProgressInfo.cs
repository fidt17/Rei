namespace ReiEditor.Models.Services.Build.Assets;

public readonly record struct AssetBuildProgressInfo(
    int CurrentAssetIndex,
    int TotalAssets,
    string AssetPath);
