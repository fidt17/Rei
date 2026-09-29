using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.DataAssets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Assets.Shaders;
using ReiEditor.Models.Services.Assets.Sync;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

public static class MonitorDrawerUtils
{
    public static BaseMonitorDrawer? CreateDrawer(
        ISelectable? selection,
        IFactory<EntityMonitorDrawerViewModel> entityMonitorFactory,
        IFactory<DataAssetMonitorDrawerViewModel> dataAssetMonitorFactory,
        IAssetsService assetsService,
        IAssetSearchService assetSearchService,
        IShaderRegistry shaderRegistry,
        IAssetRegistry assetRegistry,
        IAssetTypeMapper assetTypeMapper,
        IAssetRuntimeSyncService assetRuntimeSyncService,
        IProjectAssetFocusService projectAssetFocusService,
        out GameEntity? entityToSync)
    {
        entityToSync = null;

        if (selection is HierarchyNodeViewModel hierarchyNode)
        {
            entityToSync = hierarchyNode.Node.Content;
            return entityMonitorFactory.CreateInstance(entityToSync);
        }

        if (selection is not IAssetSelectable assetSelection)
        {
            return null;
        }

        if (AssetMonitorSupportUtility.IsTexturePreviewAsset(assetSelection.AssetPath, isDirectory: false))
        {
            return new TextureMonitorDrawerViewModel(assetSelection);
        }

        if (assetRegistry.TryGetById(assetSelection.AssetId, out var assetInfo) && AssetMonitorSupportUtility.IsDataAsset(assetInfo.FullPath, isDirectory: false, assetInfo))
        {
            return dataAssetMonitorFactory.CreateInstance(assetSelection);
        }

        if (!assetSelection.IsAssetSupportedInMonitor)
        {
            return new AssetMonitorDrawerViewModel(assetSelection);
        }

        if (!AssetMonitorSupportUtility.IsMaterialAsset(assetSelection.AssetPath, isDirectory: false))
        {
            return new AssetMonitorDrawerViewModel(assetSelection);
        }

        return new MaterialMonitorDrawerViewModel(
            assetSelection,
            assetsService,
            assetSearchService,
            shaderRegistry,
            assetRegistry,
            assetTypeMapper,
            assetRuntimeSyncService,
            projectAssetFocusService);
    }
}
