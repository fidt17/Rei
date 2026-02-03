using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Import;

namespace ReiEditor.Models.EditorApp.Refresh;

public sealed class ProjectFilesWatcherService : IDisposable
{
    private readonly IResourceService _resourceService;
    private readonly IAssetImporter _assetImporter;
    private readonly IEditorRefreshService _editorRefreshService;

    private FileSystemWatcher? _watcher;
    private Timer? _refreshTimer;
    private readonly object _watcherLock = new();
    private readonly HashSet<string> _pendingImportPaths = new(StringComparer.OrdinalIgnoreCase);

    public ProjectFilesWatcherService(
        IResourceService resourceService,
        IAssetImporter assetImporter,
        IEditorRefreshService editorRefreshService)
    {
        _resourceService = resourceService;
        _assetImporter = assetImporter;
        _editorRefreshService = editorRefreshService;

        StartWatcher();
    }

    public void Dispose()
    {
        StopWatcher();
    }

    private void StartWatcher()
    {
        var rootPath = _resourceService.GetProjectPath();
        if (string.IsNullOrWhiteSpace(rootPath)) return;
        if (!Directory.Exists(rootPath)) return;
        if (_watcher != null) return;

        _refreshTimer = new Timer(_ => ProcessWatcherRefresh(), null, Timeout.Infinite, Timeout.Infinite);

        _watcher = new FileSystemWatcher(rootPath)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
        };

        _watcher.Created += HandleWatcherEvent;
        _watcher.Deleted += HandleWatcherEvent;
        _watcher.Renamed += HandleWatcherEvent;
    }

    private void StopWatcher()
    {
        if (_watcher != null)
        {
            _watcher.Created -= HandleWatcherEvent;
            _watcher.Deleted -= HandleWatcherEvent;
            _watcher.Renamed -= HandleWatcherEvent;
            _watcher.Dispose();
            _watcher = null;
        }

        _refreshTimer?.Dispose();
        _refreshTimer = null;
    }

    private void HandleWatcherEvent(object? sender, FileSystemEventArgs e)
    {
        QueueImportPath(e.FullPath);
        _refreshTimer?.Change(200, Timeout.Infinite);
    }

    private void QueueImportPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        lock (_watcherLock)
        {
            _pendingImportPaths.Add(path);
        }
    }

    private void ProcessWatcherRefresh()
    {
        List<string> paths;
        lock (_watcherLock)
        {
            if (_pendingImportPaths.Count == 0) return;
            paths = _pendingImportPaths.ToList();
            _pendingImportPaths.Clear();
        }

        _ = Task.Run(async () =>
        {
            await _assetImporter.ReimportPaths(paths);
            _editorRefreshService.NotifyRefreshed();
        });
    }
}
