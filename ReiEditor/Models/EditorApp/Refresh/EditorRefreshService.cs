using System;
using ReiEditor.Models.Services.Assets.Import;

namespace ReiEditor.Models.EditorApp.Refresh;

public class EditorRefreshService : IEditorRefreshService, IDisposable
{
    public event Action? RefreshedEvent;

    private readonly IAssetImporter _assetImporter;

    public EditorRefreshService(IAssetImporter assetImporter)
    {
        _assetImporter = assetImporter;
        
        _assetImporter.ImportedAssetsEvent += HandleImportedAssetsEvent;
    }

    public void Dispose()
    {
        _assetImporter.ImportedAssetsEvent -= HandleImportedAssetsEvent;
    }

    private void HandleImportedAssetsEvent()
    {
        NotifyRefreshed();
    }

    public void NotifyRefreshed()
    {
        RefreshedEvent?.Invoke();
    }
}
