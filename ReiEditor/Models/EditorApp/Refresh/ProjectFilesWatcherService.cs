using System;
using System.IO;
using ReiEditor.Models.EditorApp.MainWindow;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Resources.Client;

namespace ReiEditor.Models.EditorApp.Refresh;

public sealed class ProjectFilesWatcherService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly object _watcherLock = new();
    private bool _isMainWindowActive = true;
    private bool _hasPendingChangesWhileInactive;
    
    private readonly IResourceService _resourceService;
    private readonly IMainWindowService _mainWindowService;

    public ProjectFilesWatcherService(IResourceService resourceService, IMainWindowService mainWindowService)
    {
        _resourceService = resourceService;
        _mainWindowService = mainWindowService;
        
        _mainWindowService.ActivatedEvent += HandleMainWindowActivated;
        _mainWindowService.DeactivatedEvent += HandleMainWindowDeactivated;

        StartWatcher();
    }

    public void Dispose()
    {
        StopWatcher();
        
        _mainWindowService.ActivatedEvent -= HandleMainWindowActivated;
        _mainWindowService.DeactivatedEvent -= HandleMainWindowDeactivated;
    }

    private void HandleMainWindowActivated()
    {
        lock (_watcherLock)
        {
            _isMainWindowActive = true;
        }
    }

    private void HandleMainWindowDeactivated()
    {
        lock (_watcherLock)
        {
            _isMainWindowActive = false;
        }
    }

    public bool ConsumePendingChangesWhileInactive()
    {
        lock (_watcherLock)
        {
            if (!_hasPendingChangesWhileInactive) return false;

            _hasPendingChangesWhileInactive = false;
            return true;
        }
    }

    private void StartWatcher()
    {
        var rootPath = _resourceService.GetProjectPath();
        if (string.IsNullOrWhiteSpace(rootPath)) return;
        if (!Directory.Exists(rootPath)) return;
        if (_watcher != null) return;

        _watcher = new FileSystemWatcher(rootPath)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
        };

        _watcher.Created += HandleWatcherEvent;
        _watcher.Deleted += HandleWatcherEvent;
        _watcher.Renamed += HandleWatcherEvent;
        _watcher.Changed += HandleWatcherEvent;
    }

    private void StopWatcher()
    {
        if (_watcher != null)
        {
            _watcher.Created -= HandleWatcherEvent;
            _watcher.Deleted -= HandleWatcherEvent;
            _watcher.Renamed -= HandleWatcherEvent;
            _watcher.Changed -= HandleWatcherEvent;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    private void HandleWatcherEvent(object? sender, FileSystemEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.FullPath)) return;
        if (Path.GetExtension(e.FullPath).Equals(FileExtensions.SCENE, StringComparison.OrdinalIgnoreCase)) return;

        lock (_watcherLock)
        {
            if (_isMainWindowActive) return;
            _hasPendingChangesWhileInactive = true;
        }
    }
}
