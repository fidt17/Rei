using ReiEditor.Models.Services.Assets;
using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Infrastructure.TestDoubles;

/// <summary>Controls project save state and rejects asset operations not configured by these tests.</summary>
internal sealed class TestAssetsService : IAssetsService
{
    public Observable<bool> Saving { get; } = new(false);
    public ReiEditor.Utils.Common.IObservable<bool> SaveInProcess => Saving;
    public Func<Task>? OnSave { get; set; }
    public Task SaveProject() => (OnSave ?? throw new NotSupportedException())();
    public Task<T?> Load<T>(string assetId) where T : Asset => throw new NotSupportedException();
    public Task<T?> LoadFrom<T>(string projectPath) where T : Asset => throw new NotSupportedException();
    public Task<T?> Load<T>(AssetInfo info) where T : Asset => throw new NotSupportedException();
    public void Unload(string assetId) => throw new NotSupportedException();
    public Task ReloadLoadedAssetsFromDisk(IReadOnlyCollection<string> ignoredExtensions) => throw new NotSupportedException();
}
