using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets.Migrations;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets.Migrations;

/// <summary>
/// Verifies file migration persistence, no-op behavior, and logged failures inside an isolated directory.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Migrations")]
public sealed class AssetSerializerMigrationFileTests
{
    /// <summary>
    /// A legacy scene is rewritten once, preserves its payload, and reports one successful migration.
    /// </summary>
    [Fact]
    public async Task LegacyFileIsUpdatedOnlyOnce()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("Legacy.scene");
        await File.WriteAllTextAsync(path, "{\"Unknown\":[1,null,true]}");
        var logger = new TestLogger<AssetSerializerMigrationService>();
        var service = new AssetSerializerMigrationService(logger, [new MigrateScene_0_1()]);

        Assert.True(await service.TryMigrateAssetFile(path));
        var migrated = await File.ReadAllTextAsync(path);
        Assert.True(JToken.DeepEquals(JObject.Parse("{\"Unknown\":[1,null,true],\"SerializerVersion\":1}"), JObject.Parse(migrated)));
        Assert.False(await service.TryMigrateAssetFile(path));
        Assert.Equal(migrated, await File.ReadAllTextAsync(path));
        var entry = Assert.Single(logger.Entries);
        Assert.Null(entry.Exception);
        Assert.Contains(path, entry.Message);
        Assert.Contains("from serializer version 0 to 1", entry.Message);
    }

    /// <summary>
    /// A current-version scene is not rewritten, preserving its exact bytes and producing no migration log.
    /// </summary>
    [Fact]
    public async Task CurrentFilePreservesBytes()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("Current.scene");
        var bytes = System.Text.Encoding.UTF8.GetBytes("{ \"SerializerVersion\" : 1 }\r\n");
        await File.WriteAllBytesAsync(path, bytes);
        var logger = new TestLogger<AssetSerializerMigrationService>();
        var service = new AssetSerializerMigrationService(logger, []);

        Assert.False(await service.TryMigrateAssetFile(path));

        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        Assert.Empty(logger.Entries);
    }

    /// <summary>
    /// Missing supported files and existing unsupported files return false without creating or changing files.
    /// </summary>
    [Fact]
    public async Task MissingAndUnsupportedFilesAreIgnored()
    {
        using var directory = new TemporaryDirectory();
        var missing = directory.GetPath("Missing.scene");
        var unsupported = directory.GetPath("Shader.rshader");
        const string SOURCE = "uniform float exposure;";
        await File.WriteAllTextAsync(unsupported, SOURCE);
        var logger = new TestLogger<AssetSerializerMigrationService>();
        var service = new AssetSerializerMigrationService(logger, []);

        Assert.False(await service.TryMigrateAssetFile(missing));
        Assert.False(await service.TryMigrateAssetFile(unsupported));

        Assert.False(File.Exists(missing));
        Assert.Equal(SOURCE, await File.ReadAllTextAsync(unsupported));
        Assert.Empty(logger.Entries);
    }

    /// <summary>
    /// Invalid JSON fails without replacing the source file and records the caught exception.
    /// </summary>
    [Fact]
    public async Task InvalidJsonIsPreservedAndLogged()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.GetPath("Invalid.scene");
        const string SOURCE = "{broken";
        await File.WriteAllTextAsync(path, SOURCE);
        var logger = new TestLogger<AssetSerializerMigrationService>();
        var service = new AssetSerializerMigrationService(logger, [new MigrateScene_0_1()]);

        Assert.False(await service.TryMigrateAssetFile(path));

        Assert.Equal(SOURCE, await File.ReadAllTextAsync(path));
        Assert.IsType<Newtonsoft.Json.JsonReaderException>(Assert.Single(logger.Entries).Exception);
    }
}
