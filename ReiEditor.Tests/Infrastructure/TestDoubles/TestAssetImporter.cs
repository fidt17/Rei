using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

/// <summary>Controls import state and configured full-import results without filesystem watchers.</summary>
internal sealed class TestAssetImporter : IAssetImporter
{
    public event Action? ImportedAssetsEvent;
    public Observable<bool> Importing { get; } = new(false);
    public ReiEditor.Utils.Common.IObservable<bool> IsImporting => Importing;
    public Func<Task<List<AssetInfo>>>? OnImport { get; set; }
    public void PublishImported() => ImportedAssetsEvent?.Invoke();
    public Task<List<AssetInfo>> ReimportAll() => (OnImport ?? throw new NotSupportedException())();
    public Task<List<AssetInfo>> ReimportPaths(IEnumerable<string> paths) => throw new NotSupportedException();
}
