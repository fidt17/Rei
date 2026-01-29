using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Logging.Loggers;
using ReiEditor.Models.Services.Engine.Settings;
using ReiEditor.Models.Resources;

namespace ReiEditor.Models.Services.Build.Assets.Cache;

public class AssetBuildCacheService : IAssetBuildCacheService
{
    private const string CACHE_FOLDER_NAME = "Cache";
    private const string MANIFEST_FILE_NAME = "asset-cache.json";

    private readonly ILogger<AssetBuildCacheService> _logger;
    private readonly IEngineSettingsProvider _engineSettingsProvider;

    public AssetBuildCacheService(ILogger<AssetBuildCacheService> logger, IEngineSettingsProvider engineSettingsProvider)
    {
        _logger = logger;
        _engineSettingsProvider = engineSettingsProvider;
    }

    public string GetCacheDirectory(string buildFolder)
    {
        return Path.Combine(buildFolder, ResourceConstants.RESOURCES_DIR_NAME, CACHE_FOLDER_NAME);
    }

    public string GetManifestPath(string buildFolder)
    {
        return Path.Combine(GetCacheDirectory(buildFolder), MANIFEST_FILE_NAME);
    }

    public AssetBuildCacheManifest LoadOrCreateManifest(string buildFolder)
    {
        var cacheDir = GetCacheDirectory(buildFolder);
        Directory.CreateDirectory(cacheDir);

        var manifestPath = GetManifestPath(buildFolder);
        var cacheKey = GetCacheKey();
        if (!File.Exists(manifestPath))
        {
            return new AssetBuildCacheManifest
            {
                CacheKey = cacheKey
            };
        }

        try
        {
            var json = File.ReadAllText(manifestPath, Encoding.UTF8);
            var manifest = JsonConvert.DeserializeObject<AssetBuildCacheManifest>(json);
            if (manifest == null)
            {
                _logger.LogWarning("Asset cache manifest is invalid, creating a new one");
                return new AssetBuildCacheManifest { CacheKey = cacheKey };
            }

            if (!string.Equals(manifest.CacheKey, cacheKey, StringComparison.Ordinal))
            {
                _logger.Log("Asset cache key changed, creating a new manifest");
                File.Delete(manifestPath);
                return new AssetBuildCacheManifest { CacheKey = cacheKey };
            }

            return manifest;
        }
        catch (Exception e)
        {
            _logger.LogException(e);
            return new AssetBuildCacheManifest { CacheKey = cacheKey };
        }
    }

    public void SaveManifest(string buildFolder, AssetBuildCacheManifest manifest)
    {
        var cacheDir = GetCacheDirectory(buildFolder);
        Directory.CreateDirectory(cacheDir);

        var manifestPath = GetManifestPath(buildFolder);
        var json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
        File.WriteAllText(manifestPath, json, Encoding.UTF8);
    }

    public string ComputeContentHash(string assetPath)
    {
        using var stream = File.OpenRead(assetPath);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
    }

    public string GetCacheFileName(AssetInfo assetInfo, string contentHash)
    {
        return $"{assetInfo.Meta.AssetId}_{contentHash}.cache";
    }

    public string GetCacheFilePath(string buildFolder, string cacheFileName)
    {
        return Path.Combine(GetCacheDirectory(buildFolder), cacheFileName);
    }

    public bool TryGetCacheEntry(
        string buildFolder,
        AssetBuildCacheManifest manifest,
        AssetInfo assetInfo,
        string contentHash,
        out AssetBuildCacheManifest.AssetBuildCacheEntry entry,
        out string cacheFilePath)
    {
        entry = null!;
        cacheFilePath = "";

        if (!manifest.Entries.TryGetValue(assetInfo.Meta.AssetId, out var existing)) return false;
        if (!string.Equals(existing.ContentHash, contentHash, StringComparison.Ordinal)) return false;
        if (!string.Equals(existing.AssetPath, assetInfo.FullPath, StringComparison.Ordinal)) return false;

        cacheFilePath = GetCacheFilePath(buildFolder, existing.CacheFileName);
        if (!File.Exists(cacheFilePath))
        {
            manifest.Entries.Remove(assetInfo.Meta.AssetId);
            return false;
        }

        var cacheSize = new FileInfo(cacheFilePath).Length;
        if (existing.CacheSize != cacheSize)
        {
            File.Delete(cacheFilePath);
            manifest.Entries.Remove(assetInfo.Meta.AssetId);
            return false;
        }

        entry = existing;
        return true;
    }

    public AssetBuildCacheManifest.AssetBuildCacheEntry CreateEntry(
        AssetInfo assetInfo,
        string contentHash,
        string cacheFileName,
        long cacheSize)
    {
        return new AssetBuildCacheManifest.AssetBuildCacheEntry
        {
            AssetId = assetInfo.Meta.AssetId,
            AssetPath = assetInfo.FullPath,
            ContentHash = contentHash,
            CacheFileName = cacheFileName,
            CacheSize = cacheSize
        };
    }

    public void AddEntry(AssetBuildCacheManifest manifest, AssetBuildCacheManifest.AssetBuildCacheEntry entry)
    {
        manifest.Entries[entry.AssetId] = entry;
    }

    public void PruneUnusedCacheFiles(string buildFolder, AssetBuildCacheManifest manifest)
    {
        var cacheDir = GetCacheDirectory(buildFolder);
        if (!Directory.Exists(cacheDir)) return;

        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in manifest.Entries.Values)
        {
            if (!string.IsNullOrWhiteSpace(entry.CacheFileName))
            {
                expected.Add(entry.CacheFileName);
            }
        }

        foreach (var file in Directory.EnumerateFiles(cacheDir, "*.cache", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            if (expected.Contains(name)) continue;

            try
            {
                File.Delete(file);
            }
            catch (Exception e)
            {
                _logger.LogException(e);
            }
        }
    }

    private string GetCacheKey()
    {
        return AssetBuildCacheKey.Create(_engineSettingsProvider.GetEngineVersion());
    }
}
