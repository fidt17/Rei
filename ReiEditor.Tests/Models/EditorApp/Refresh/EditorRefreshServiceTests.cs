using ReiEditor.Models.EditorApp.Refresh;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Models.EditorApp.Refresh;

/// <summary>Verifies editor refresh notifications follow asset import events and disposal.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Refresh")]
public sealed class EditorRefreshServiceTests
{
    /// <summary>Publishes controlled asset import events without filesystem work.</summary>
    private sealed class TestAssetImporter : IAssetImporter
    {
        public event Action ImportedAssetsEvent = delegate { };
        public Observable<bool> Importing { get; } = new(false);
        public ReiEditor.Utils.Common.IObservable<bool> IsImporting => Importing;
        public Task<List<AssetInfo>> ReimportAll() => throw new NotSupportedException();
        public Task<List<AssetInfo>> ReimportPaths(IEnumerable<string> paths) => throw new NotSupportedException();
        public void PublishImported() => ImportedAssetsEvent?.Invoke();
    }

    /// <summary>Imported assets and explicit notifications both publish refresh events.</summary>
    [Fact]
    public void ImportedAndExplicitRefreshesPublishNotifications()
    {
        var importer = new TestAssetImporter();
        using var service = new EditorRefreshService(importer);
        var refreshes = 0;
        service.RefreshedEvent += () => refreshes++;

        importer.PublishImported();
        service.NotifyRefreshed();

        Assert.Equal(2, refreshes);
    }

    /// <summary>Disposal removes asset import subscription.</summary>
    [Fact]
    public void DisposeStopsImportedAssetRefreshes()
    {
        var importer = new TestAssetImporter();
        var service = new EditorRefreshService(importer);
        var refreshes = 0;
        service.RefreshedEvent += () => refreshes++;
        service.Dispose();

        importer.PublishImported();

        Assert.Equal(0, refreshes);
    }
}
