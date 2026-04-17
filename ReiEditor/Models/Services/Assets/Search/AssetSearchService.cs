using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IOPath = System.IO.Path;
using ReiEditor.Models.Resources.Client;
using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.Models.Services.Assets.Search;

public sealed class AssetSearchService : IAssetSearchService
{
    private readonly IResourceService _resourceService;

    public AssetSearchService(IResourceService resourceService)
    {
        _resourceService = resourceService;
    }

    public IReadOnlyList<AssetSearchResult> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<AssetSearchResult>();

        var rootPath = _resourceService.GetProjectPath();
        if (string.IsNullOrWhiteSpace(rootPath)) return Array.Empty<AssetSearchResult>();
        if (!Directory.Exists(rootPath)) return Array.Empty<AssetSearchResult>();

        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length == 0) return Array.Empty<AssetSearchResult>();

        var results = new List<AssetSearchResult>();
        TraverseDirectory(rootPath, normalizedQuery, results);

        return results
            .OrderBy(result => result.IsDirectory ? 0 : 1)
            .ThenBy(result => result.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<AssetSearchResult> SearchByExtensions(string query, IReadOnlyCollection<string> extensions)
    {
        if (extensions.Count == 0) return Array.Empty<AssetSearchResult>();

        var results = Search(query);
        if (results.Count == 0) return results;

        return results
            .Where(result => result.IsDirectory || extensions.Contains(IOPath.GetExtension(result.FullPath), StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private static void TraverseDirectory(string directoryPath, string query, ICollection<AssetSearchResult> results)
    {
        foreach (var directory in EnumerateDirectories(directoryPath))
        {
            if (AssetFileFilter.ShouldHideDirectory(directory)) continue;

            var name = IOPath.GetFileName(directory);
            if (!string.IsNullOrWhiteSpace(name) && name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new AssetSearchResult(name, directory, isDirectory: true));
            }

            TraverseDirectory(directory, query, results);
        }

        foreach (var file in EnumerateFiles(directoryPath))
        {
            if (AssetFileFilter.ShouldHide(file)) continue;

            var name = IOPath.GetFileName(file);
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!name.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;

            results.Add(new AssetSearchResult(name, file, isDirectory: false));
        }
    }

    private static IEnumerable<string> EnumerateDirectories(string directoryPath)
    {
        try
        {
            return Directory.EnumerateDirectories(directoryPath, "*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false
            });
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> EnumerateFiles(string directoryPath)
    {
        try
        {
            return Directory.EnumerateFiles(directoryPath, "*.*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false
            });
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }
}
