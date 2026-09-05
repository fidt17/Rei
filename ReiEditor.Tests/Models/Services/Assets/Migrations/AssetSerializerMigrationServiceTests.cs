using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets.Migrations;
using ReiEditor.Models.Services.Render;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets.Migrations;

/// <summary>
/// Verifies migration routing, payload preservation, version handling, and invalid migration chains.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Migrations")]
public sealed class AssetSerializerMigrationServiceTests
{
    private sealed class TestAssetSerializerMigration(Type assetType, int fromVersion, int toVersion) : IAssetSerializerMigration
    {
        public Type AssetType { get; } = assetType;
        public int FromVersion { get; } = fromVersion;
        public int ToVersion { get; } = toVersion;
        public int CallCount { get; private set; }

        public void Migrate(JObject assetJson)
        {
            CallCount++;
            assetJson["MigrationMarker"] = "applied";
        }
    }

    /// <summary>
    /// Each production legacy migration adds version one without changing unknown payload fields and is idempotent.
    /// </summary>
    [Theory]
    [InlineData(typeof(Scene), typeof(MigrateScene_0_1))]
    [InlineData(typeof(Material), typeof(MigrateMaterial_0_1))]
    [InlineData(typeof(Shader), typeof(MigrateShader_0_1))]
    [InlineData(typeof(BuildScenesConfiguration), typeof(MigrateBuildScenesConfiguration_0_1))]
    public void ProductionMigrationPreservesPayloadAndBecomesNoOp(Type assetType, Type migrationType)
    {
        const string SOURCE = """{"Name":"Legacy","Unknown":{"Items":[1,null,{"Flag":true}],"Unicode":"雪"}}""";
        var migration = Assert.IsAssignableFrom<IAssetSerializerMigration>(Activator.CreateInstance(migrationType));
        Assert.Equal(assetType, migration.AssetType);
        Assert.Equal(0, migration.FromVersion);
        Assert.Equal(1, migration.ToVersion);
        var service = CreateService(migration);

        var result = service.MigrateAssetJson(assetType, SOURCE);

        Assert.True(result.IsUpdated);
        Assert.Equal(0, result.SourceVersion);
        Assert.Equal(1, result.TargetVersion);
        var expected = JObject.Parse(SOURCE);
        expected["SerializerVersion"] = 1;
        Assert.True(JToken.DeepEquals(expected, JObject.Parse(result.Json)));
        Assert.Equal(new AssetSerializerMigrationResult(result.Json, 1, 1, false), service.MigrateAssetJson(assetType, result.Json));
    }

    /// <summary>
    /// Current-version JSON is returned verbatim and never invokes a legacy migration.
    /// </summary>
    [Fact]
    public void CurrentVersionPreservesOriginalFormattingWithoutRunningMigration()
    {
        const string SOURCE = "{ \"SerializerVersion\" : 1,\r\n \"Unknown\" : [ null, 2 ] } ";
        var migration = new TestAssetSerializerMigration(typeof(Scene), 0, 1);

        var result = CreateService(migration).MigrateAssetJson(typeof(Scene), SOURCE);

        Assert.Equal(new AssetSerializerMigrationResult(SOURCE, 1, 1, false), result);
        Assert.Equal(0, migration.CallCount);
    }

    /// <summary>
    /// Explicit legacy coercion treats negative and non-integer version tokens as version zero.
    /// </summary>
    [Theory]
    [InlineData("-1")]
    [InlineData("\"1\"")]
    [InlineData("null")]
    public void LegacyVersionCoercionRunsMatchingMigration(string versionToken)
    {
        var migration = new TestAssetSerializerMigration(typeof(Scene), 0, 1);
        var unrelated = new TestAssetSerializerMigration(typeof(Material), 0, 1);
        var source = "{\"SerializerVersion\":" + versionToken + ",\"Keep\":42}";

        var result = CreateService(unrelated, migration).MigrateAssetJson(typeof(Scene), source);

        Assert.Equal(0, result.SourceVersion);
        Assert.Equal(1, result.TargetVersion);
        Assert.True(result.IsUpdated);
        Assert.Equal(1, migration.CallCount);
        Assert.Equal(0, unrelated.CallCount);
        var expected = JObject.Parse("""{"SerializerVersion":1,"Keep":42,"MigrationMarker":"applied"}""");
        Assert.True(JToken.DeepEquals(expected, JObject.Parse(result.Json)));
    }

    /// <summary>
    /// A future serializer version is rejected before any migration is invoked.
    /// </summary>
    [Fact]
    public void FutureVersionIsRejectedBeforeMigration()
    {
        var migration = new TestAssetSerializerMigration(typeof(Scene), 0, 1);

        var exception = Assert.Throws<Exception>(() => CreateService(migration).MigrateAssetJson(typeof(Scene), "{\"SerializerVersion\":2}"));

        Assert.Contains("unsupported serializer version 2", exception.Message);
        Assert.Equal(0, migration.CallCount);
    }

    /// <summary>
    /// Missing input and non-object JSON are rejected with the corresponding asset diagnostic.
    /// </summary>
    [Theory]
    [InlineData(" \r\n", "Asset JSON is missing")]
    [InlineData("[]", "Asset JSON root must be an object")]
    [InlineData("null", "Asset JSON root must be an object")]
    public void MissingOrNonObjectJsonIsRejected(string source, string diagnostic)
    {
        var exception = Assert.Throws<Exception>(() => CreateService().MigrateAssetJson(typeof(Scene), source));

        Assert.Contains(diagnostic, exception.Message);
        Assert.Contains(nameof(Scene), exception.Message);
    }

    /// <summary>
    /// Malformed JSON preserves the parser failure instead of attempting a migration.
    /// </summary>
    [Fact]
    public void MalformedJsonThrowsParserException()
    {
        Assert.Throws<JsonReaderException>(() => CreateService().MigrateAssetJson(typeof(Scene), "{broken"));
    }

    /// <summary>
    /// A null asset type is rejected before parsing.
    /// </summary>
    [Fact]
    public void NullAssetTypeIsRejected()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => CreateService().MigrateAssetJson(null!, "{}"));

        Assert.Equal("assetType", exception.ParamName);
    }

    /// <summary>
    /// Unversioned asset types explicitly bypass JSON parsing and return the input unchanged.
    /// </summary>
    [Fact]
    public void UnknownAssetTypeBypassesParsing()
    {
        const string SOURCE = "not JSON";

        var result = CreateService().MigrateAssetJson(typeof(object), SOURCE);

        Assert.Equal(new AssetSerializerMigrationResult(SOURCE, 0, 0, false), result);
    }

    /// <summary>
    /// Both an absent migration set and a set missing the required source version report the missing chain.
    /// </summary>
    [Theory]
    [InlineData(false, "Missing serializer migration set")]
    [InlineData(true, "Missing serializer migration for")]
    public void MissingMigrationChainIsRejected(bool hasOtherVersion, string diagnostic)
    {
        var migration = new TestAssetSerializerMigration(typeof(Scene), 1, 2);
        var service = hasOtherVersion ? CreateService(migration) : CreateService();

        var exception = Assert.Throws<Exception>(() => service.MigrateAssetJson(typeof(Scene), "{}"));

        Assert.Contains(diagnostic, exception.Message);
        Assert.Contains("0 -> 1", exception.Message);
        Assert.Equal(0, migration.CallCount);
    }

    /// <summary>
    /// Two migrations starting at the same version for the same asset type are rejected during construction.
    /// </summary>
    [Fact]
    public void DuplicateSourceVersionIsRejected()
    {
        var first = new TestAssetSerializerMigration(typeof(Scene), 0, 1);
        var duplicate = new TestAssetSerializerMigration(typeof(Scene), 0, 2);

        var exception = Assert.Throws<Exception>(() => CreateService(first, duplicate));

        Assert.Contains("Duplicate migration for Scene from version 0", exception.Message);
    }

    /// <summary>
    /// Migration ranges must advance the serializer version to prevent a stalled or backwards chain.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonIncreasingMigrationRangeIsRejected(int targetVersion)
    {
        var migration = new TestAssetSerializerMigration(typeof(Scene), 0, targetVersion);

        var exception = Assert.Throws<Exception>(() => CreateService(migration));

        Assert.Contains($"Invalid migration range for Scene: 0 -> {targetVersion}", exception.Message);
    }

    private static AssetSerializerMigrationService CreateService(params IAssetSerializerMigration[] migrations)
    {
        return new AssetSerializerMigrationService(new TestLogger<AssetSerializerMigrationService>(), migrations);
    }
}
