using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Logging.Loggers;

namespace ReiEditor.Models.Services.Assets.Migrations;

public class AssetSerializerMigrationService : IAssetSerializerMigrationService
{
    private const string SERIALIZER_VERSION_PROPERTY = "SerializerVersion";

    private readonly ILogger<AssetSerializerMigrationService> _logger;
    private readonly Dictionary<Type, Dictionary<int, IAssetSerializerMigration>> _migrationsByType = new();

    public AssetSerializerMigrationService(ILogger<AssetSerializerMigrationService> logger, IEnumerable<IAssetSerializerMigration> migrations)
    {
        _logger = logger;
        BuildMigrationIndex(migrations);
    }

    public AssetSerializerMigrationResult MigrateAssetJson(Type assetType, string sourceJson)
    {
        if (assetType == null) throw new ArgumentNullException(nameof(assetType));
        if (string.IsNullOrWhiteSpace(sourceJson)) throw new Exception($"Asset JSON is missing for {assetType.Name}");

        var currentVersion = AssetSerializerVersions.GetCurrentVersion(assetType);
        if (currentVersion <= AssetSerializerVersions.LEGACY_VERSION)
        {
            return new AssetSerializerMigrationResult(sourceJson, AssetSerializerVersions.LEGACY_VERSION, currentVersion, false);
        }

        var rootToken = JToken.Parse(sourceJson);
        if (rootToken is not JObject rootObject)
        {
            throw new Exception($"Asset JSON root must be an object for {assetType.Name}");
        }

        var sourceVersion = ReadSerializerVersion(rootObject);
        var migratedVersion = sourceVersion;
        var updated = false;

        if (sourceVersion > currentVersion)
        {
            throw new Exception($"Asset {assetType.Name} has unsupported serializer version {sourceVersion}. Current version is {currentVersion}.");
        }

        while (migratedVersion < currentVersion)
        {
            var migration = ResolveMigration(assetType, migratedVersion);
            migration.Migrate(rootObject);
            migratedVersion = migration.ToVersion;
            updated = true;
        }

        if (ReadSerializerVersion(rootObject) != currentVersion)
        {
            rootObject[SERIALIZER_VERSION_PROPERTY] = currentVersion;
            updated = true;
        }

        if (!updated)
        {
            return new AssetSerializerMigrationResult(sourceJson, sourceVersion, migratedVersion, false);
        }

        return new AssetSerializerMigrationResult(rootObject.ToString(Formatting.Indented), sourceVersion, migratedVersion, true);
    }

    public async Task<bool> TryMigrateAssetFile(string assetPath)
    {
        try
        {
            if (!AssetSerializerMigrationTargetResolver.TryResolveAssetType(assetPath, out var assetType)) return false;
            if (!File.Exists(assetPath)) return false;

            var sourceJson = await File.ReadAllTextAsync(assetPath);
            var migrationResult = MigrateAssetJson(assetType, sourceJson);
            if (!migrationResult.IsUpdated) return false;

            await File.WriteAllTextAsync(assetPath, migrationResult.Json);
            _logger.Log($"Migrated asset '{assetPath}' from serializer version {migrationResult.SourceVersion} to {migrationResult.TargetVersion}");
            return true;
        }
        catch (Exception e)
        {
            _logger.LogException(e);
            return false;
        }
    }

    private void BuildMigrationIndex(IEnumerable<IAssetSerializerMigration> migrations)
    {
        foreach (var migration in migrations)
        {
            if (!_migrationsByType.TryGetValue(migration.AssetType, out var byVersion))
            {
                byVersion = new Dictionary<int, IAssetSerializerMigration>();
                _migrationsByType[migration.AssetType] = byVersion;
            }

            if (!byVersion.TryAdd(migration.FromVersion, migration))
            {
                throw new Exception($"Duplicate migration for {migration.AssetType.Name} from version {migration.FromVersion}");
            }

            if (migration.ToVersion <= migration.FromVersion)
            {
                throw new Exception($"Invalid migration range for {migration.AssetType.Name}: {migration.FromVersion} -> {migration.ToVersion}");
            }
        }
    }

    private IAssetSerializerMigration ResolveMigration(Type assetType, int fromVersion)
    {
        if (!_migrationsByType.TryGetValue(assetType, out var byVersion))
        {
            throw new Exception($"Missing serializer migration set for {assetType.Name} (required {fromVersion} -> {fromVersion + 1}).");
        }

        if (!byVersion.TryGetValue(fromVersion, out var migration))
        {
            throw new Exception($"Missing serializer migration for {assetType.Name} version {fromVersion} -> {fromVersion + 1}.");
        }

        return migration;
    }

    private static int ReadSerializerVersion(JObject rootObject)
    {
        var token = rootObject[SERIALIZER_VERSION_PROPERTY];
        if (token == null) return AssetSerializerVersions.LEGACY_VERSION;
        if (token.Type != JTokenType.Integer) return AssetSerializerVersions.LEGACY_VERSION;

        var parsed = token.Value<int>();
        return parsed < AssetSerializerVersions.LEGACY_VERSION ? AssetSerializerVersions.LEGACY_VERSION : parsed;
    }
}
