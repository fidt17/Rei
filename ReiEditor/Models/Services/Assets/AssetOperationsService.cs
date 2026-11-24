using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.DataAssets;
using ReiEditor.Models.Services.FileSystem;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Utils.Path;

namespace ReiEditor.Models.Services.Assets;

public class AssetOperationsService : IAssetOperationsService
{
    private readonly ILogger<AssetOperationsService> _logger;
    private readonly IResourceService _resourceService;
    private readonly IAssetImporter _assetImporter;
    private readonly IAssetRegistry _assetRegistry;
    private readonly IMetaFilesService _metaFilesService;
    private readonly IBehaviourRegistry _behaviourRegistry;
    private readonly IDataAssetTypeRegistry _dataAssetTypeRegistry;

    public AssetOperationsService(
        ILogger<AssetOperationsService> logger,
        IResourceService resourceService,
        IAssetImporter assetImporter,
        IAssetRegistry assetRegistry,
        IMetaFilesService metaFilesService,
        IBehaviourRegistry behaviourRegistry,
        IDataAssetTypeRegistry dataAssetTypeRegistry)
    {
        _logger = logger;
        _resourceService = resourceService;
        _assetImporter = assetImporter;
        _assetRegistry = assetRegistry;
        _metaFilesService = metaFilesService;
        _behaviourRegistry = behaviourRegistry;
        _dataAssetTypeRegistry = dataAssetTypeRegistry;
    }

    public Task RenameAsync(string assetPath, string newName)
    {
        return RunWithErrorHandling(() =>
        {
            _logger.Log($"Rename requested. Path: {assetPath}. New name: {newName}");
            var sourceIsFile = File.Exists(assetPath);
            var sourceIsDirectory = Directory.Exists(assetPath);
            if (!sourceIsFile && !sourceIsDirectory)
            {
                _logger.LogWarning($"Rename skipped: source path does not exist: {assetPath}");
                return Task.CompletedTask;
            }
            
            var trimmed = newName.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                _logger.LogWarning("Rename skipped: new name is empty.");
                return Task.CompletedTask;
            }

            var directoryName = Path.GetDirectoryName(assetPath);
            if (string.IsNullOrWhiteSpace(directoryName))
            {
                _logger.LogWarning($"Rename skipped: could not resolve directory for {assetPath}");
                return Task.CompletedTask;
            }

            var targetPath = Path.Combine(directoryName, trimmed);
            if (assetPath.PathEquals(targetPath))
            {
                _logger.Log("Rename skipped: target path is the same as source.");
                return Task.CompletedTask;
            }

            if (sourceIsDirectory)
            {
                if (Directory.Exists(targetPath))
                {
                    _logger.LogWarning($"Rename skipped: target directory already exists at {targetPath}");
                    return Task.CompletedTask;
                }
                
                Directory.Move(assetPath, targetPath);
                _logger.Log($"Renamed directory: {assetPath} -> {targetPath}");
            }
            else
            {
                if (File.Exists(targetPath))
                {
                    _logger.LogWarning($"Rename skipped: target file already exists at {targetPath}");
                    return Task.CompletedTask;
                }
                
                File.Move(assetPath, targetPath);
                _metaFilesService.MoveMetaFile(assetPath, targetPath);
                _logger.Log($"Renamed file: {assetPath} -> {targetPath}");
            }
            
            _assetRegistry.UpdateRegistryPath(assetPath, targetPath);
            
            return Task.CompletedTask;
        });
    }

    public Task DeleteAsync(string assetPath, bool isDirectory)
    {
        return RunWithErrorHandling(() =>
        {
            if (isDirectory)
            {
                if (!Directory.Exists(assetPath)) return Task.CompletedTask;
                
                Directory.Delete(assetPath, recursive: true);
                _assetRegistry.UnregisterUnderDirectory(assetPath);
            }
            else
            {
                if (!File.Exists(assetPath)) return Task.CompletedTask;
                
                File.Delete(assetPath);
                _metaFilesService.DeleteMetaFile(assetPath);
                _assetRegistry.UnregisterByPath(assetPath);
            }

            return Task.CompletedTask;
        });
    }

    public Task DuplicateAsync(string assetPath, bool isDirectory)
    {
        return RunWithErrorHandling(async () =>
        {
            var targetPath = PathNamingUtils.GetDuplicatePath(assetPath, isDirectory);

            if (isDirectory)
            {
                if (Directory.Exists(targetPath)) return;
                
                _resourceService.CopyFilesRecursively(assetPath, targetPath);
                await _metaFilesService.RegenerateMetaFilesInDirectory(
                    targetPath,
                    GetRegenerationPolicy());
            }
            else
            {
                if (File.Exists(targetPath)) return;
                
                File.Copy(assetPath, targetPath);
                await _metaFilesService.RegenerateMetaFileForAsset(
                    targetPath,
                    GetRegenerationPolicy());
            }

            await _assetImporter.ReimportPaths(new[] { targetPath });
        });
    }

    public Task<string?> MoveAsync(string assetPath, string destinationFolder)
    {
        return RunMoveAsync(() =>
        {
            if (!Directory.Exists(destinationFolder) || destinationFolder.PathEquals(Path.GetDirectoryName(assetPath) ?? assetPath)) return null;
            var name = Path.GetFileName(assetPath);
            var targetPath = Directory.Exists(assetPath)
                ? PathNamingUtils.GetUniqueDirectoryPath(destinationFolder, name)
                : PathNamingUtils.GetUniqueFilePath(destinationFolder, name);
            return MoveToPath(assetPath, targetPath) ? targetPath : null;
        });
    }

    public bool CanMoveToPath(string assetPath, string targetPath)
    {
        if (!Directory.Exists(Path.GetDirectoryName(targetPath)) || File.Exists(targetPath) || Directory.Exists(targetPath)) return false;
        if (Directory.Exists(assetPath)) return !targetPath.IsUnderDirectory(assetPath);
        return File.Exists(assetPath) && !File.Exists(targetPath + FileExtensions.META);
    }

    public async Task<bool> MoveToPathAsync(string assetPath, string targetPath)
    {
        return await RunMoveAsync(() => MoveToPath(assetPath, targetPath) ? targetPath : null) != null;
    }

    private bool MoveToPath(string assetPath, string targetPath)
    {
        if (!CanMoveToPath(assetPath, targetPath)) return false;
        if (Directory.Exists(assetPath))
        {
            Directory.Move(assetPath, targetPath);
        }
        else
        {
            File.Move(assetPath, targetPath);
            try { _metaFilesService.MoveMetaFile(assetPath, targetPath); }
            catch
            {
                File.Move(targetPath, assetPath);
                throw;
            }
        }

        _assetRegistry.UpdateRegistryPath(assetPath, targetPath);
        return true;
    }

    private async Task<string?> RunMoveAsync(Func<string?> move)
    {
        try { return await Task.Run(move); }
        catch (Exception error) { _logger.LogException(error); return null; }
    }

    public Task ImportExternalAssets(IEnumerable<string> sourcePaths, string targetFolder)
    {
        return RunWithErrorHandling(async () =>
        {
            if (string.IsNullOrWhiteSpace(targetFolder) || !Directory.Exists(targetFolder)) return;

            var createdPaths = new List<string>();

            foreach (var sourcePath in sourcePaths)
            {
                if (string.IsNullOrWhiteSpace(sourcePath)) continue;

                if (Directory.Exists(sourcePath))
                {
                    var dirName = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    var destination = PathNamingUtils.GetUniqueDirectoryPath(targetFolder, dirName);
                    _resourceService.CopyFilesRecursively(sourcePath, destination);
                    createdPaths.Add(destination);
                    continue;
                }

                if (File.Exists(sourcePath))
                {
                    var fileName = Path.GetFileName(sourcePath);
                    var destination = PathNamingUtils.GetUniqueFilePath(targetFolder, fileName);
                    File.Copy(sourcePath, destination);
                    createdPaths.Add(destination);
                }
            }

            if (createdPaths.Count > 0)
            {
                await _metaFilesService.RegenerateMetaFilesForTargets(
                    createdPaths,
                    GetRegenerationPolicy());
                await _assetImporter.ReimportPaths(createdPaths);
            }
        });
    }

    private IMetaFileRegenerationPolicy GetRegenerationPolicy()
    {
        return new CompositeMetaFileRegenerationPolicy(
            new BehaviourMetaFileRegenerationPolicy(_behaviourRegistry.AllocateBehaviourId),
            new DataAssetMetaFileRegenerationPolicy(_dataAssetTypeRegistry.AllocateDataAssetTypeId));
    }

    public Task CreateFolderAsync(string parentDirectory, string folderName)
    {
        return RunWithErrorHandling(() =>
        {
            if (string.IsNullOrWhiteSpace(parentDirectory) 
                || !Directory.Exists(parentDirectory) 
                || string.IsNullOrWhiteSpace(folderName)) return Task.CompletedTask;

            var newFolderPath = PathNamingUtils.GetUniqueDirectoryPath(parentDirectory, folderName);
            Directory.CreateDirectory(newFolderPath);
            
            return Task.CompletedTask;
        });
    }

    private async Task RunWithErrorHandling(Func<Task> action)
    {
        try
        {
            await Task.Run(action);
        }
        catch (Exception e)
        {
            _logger.LogException(e);
        }
    }
}
