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

        var results = new List<AssetSearchResult>();
        var comparison = StringComparison.OrdinalIgnoreCase;

        foreach (var directory in Directory.EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories))
        {
            var name = IOPath.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (name.IndexOf(query, comparison) < 0) continue;

            results.Add(new AssetSearchResult(name, directory, isDirectory: true));
        }

        foreach (var file in Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories))
        {
            if (AssetFileFilter.ShouldHide(file)) continue;

            var name = IOPath.GetFileName(file);
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (name.IndexOf(query, comparison) < 0) continue;

            results.Add(new AssetSearchResult(name, file, isDirectory: false));
        }

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
}
