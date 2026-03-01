using ReiEditor.Models.EditorApp.Selection;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Search;
using ReiEditor.Models.Services.Entities;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Windows.Editor.Hierarchies;

namespace ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers;

public static class MonitorDrawerUtils
{
    public static BaseMonitorDrawer? CreateDrawer(
        ISelectable? selection,
        IFactory<EntityMonitorDrawerViewModel> entityMonitorFactory,
        IAssetsService assetsService,
        IAssetSearchService assetSearchService,
        IAssetRegistry assetRegistry,
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

        if (!assetSelection.IsAssetSupportedInMonitor)
        {
            return new AssetMonitorDrawerViewModel(assetSelection);
        }

        return new MaterialMonitorDrawerViewModel(assetSelection, assetsService, assetSearchService, assetRegistry);
    }
}
