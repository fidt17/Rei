using ReiEditor.Models.Services.Assets;
using ReiEditor.Models.Services.Assets.Meta;
using ReiEditor.Models.Services.Assets.Sync;
using ReiEditor.Models.Services.Engine.Api;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets.Sync;

/// <summary>
/// Verifies runtime asset get, set, and patch routing through resolved asset types.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Assets")]
public sealed class AssetRuntimeSyncServiceTests
{
    /// <summary>
    /// Captures runtime asset API calls and returns configured results.
    /// </summary>
    private sealed class TestAssetApi : IAssetApi
    {
        public bool Result { get; set; } = true;
        public string GetResponse { get; set; } = "";
        public Exception? ExceptionToThrow { get; set; }
        public List<(string Operation, string AssetId, string AssetType, string Data)> Calls { get; } = new();

        public bool TryGetAssetData(string assetId, string assetType, out string jsonData)
        {
            ThrowIfConfigured();
            Calls.Add(("get", assetId, assetType, ""));
            jsonData = GetResponse;
            return Result;
        }

        public bool TrySetAssetData(string assetId, string assetType, string jsonData)
        {
            ThrowIfConfigured();
            Calls.Add(("set", assetId, assetType, jsonData));
            return Result;
        }

        public bool TryPatchAssetData(string assetId, string assetType, string jsonPatch)
        {
            ThrowIfConfigured();
            Calls.Add(("patch", assetId, assetType, jsonPatch));
            return Result;
        }

        private void ThrowIfConfigured()
        {
            if (ExceptionToThrow != null) throw ExceptionToThrow;
        }
    }

    /// <summary>
    /// Get passes asset ID and resolved material type and preserves API response.
    /// </summary>
    [Fact]
    public void TryGetAssetDataPassesResolvedTypeAndResponse()
    {
        using var fixture = new TemporaryProjectFixture();
        var api = new TestAssetApi { GetResponse = "{\"value\":1}" };
        var service = CreateService(fixture, api, out _);

        var result = service.TryGetAssetData("material", out var json);

        Assert.True(result);
        Assert.Equal("{\"value\":1}", json);
        Assert.Equal(("get", "material", "Material", ""), Assert.Single(api.Calls));
    }

    /// <summary>
    /// Set and patch pass JSON text unchanged with resolved material type.
    /// </summary>
    [Fact]
    public void TrySetAndPatchAssetDataPreservePayloads()
    {
        using var fixture = new TemporaryProjectFixture();
        var api = new TestAssetApi();
        var service = CreateService(fixture, api, out _);

        Assert.True(service.TrySetAssetData("material", " {\"set\":true} "));
        Assert.True(service.TryPatchAssetData("material", "[{\"op\":\"remove\"}]"));

        Assert.Equal(("set", "material", "Material", " {\"set\":true} "), api.Calls[0]);
        Assert.Equal(("patch", "material", "Material", "[{\"op\":\"remove\"}]"), api.Calls[1]);
    }

    /// <summary>
    /// Blank, unknown, and unsupported asset IDs fail without invoking runtime API.
    /// </summary>
    [Fact]
    public void UnsupportedAssetIdsDoNotInvokeApi()
    {
        using var fixture = new TemporaryProjectFixture();
        var api = new TestAssetApi();
        var service = CreateService(fixture, api, out var registry);
        registry.RegisterNewAssets(new[]
        {
            new AssetInfo(new AssetMeta("scene"), fixture.Directory.GetPath("Project", "scene.scene"))
        });

        Assert.False(service.TryGetAssetData("", out var blankJson));
        Assert.Equal("", blankJson);
        Assert.False(service.TryGetAssetData("missing", out var missingJson));
        Assert.Equal("", missingJson);
        Assert.False(service.TrySetAssetData("scene", "{}"));
        Assert.False(service.TryPatchAssetData("scene", "[]"));
        Assert.Empty(api.Calls);
    }

    /// <summary>
    /// API false result is returned and logged for get, set, and patch operations.
    /// </summary>
    [Fact]
    public void ApiFailureReturnsFalseAndLogsEachOperation()
    {
        using var fixture = new TemporaryProjectFixture();
        var api = new TestAssetApi { Result = false, GetResponse = "partial" };
        var service = CreateService(fixture, api, out _, out var logger);

        Assert.False(service.TryGetAssetData("material", out var json));
        Assert.Equal("partial", json);
        Assert.False(service.TrySetAssetData("material", "{}"));
        Assert.False(service.TryPatchAssetData("material", "[]"));
        Assert.Equal(3, logger.Entries.Count(entry => entry.Level == LogLevelEnum.Warning));
    }

    /// <summary>
    /// Runtime API exceptions propagate to caller.
    /// </summary>
    [Fact]
    public void ApiExceptionPropagates()
    {
        using var fixture = new TemporaryProjectFixture();
        var expected = new InvalidOperationException("runtime unavailable");
        var service = CreateService(fixture, new TestAssetApi { ExceptionToThrow = expected }, out _);

        var actual = Assert.Throws<InvalidOperationException>(() => service.TrySetAssetData("material", "{}"));

        Assert.Same(expected, actual);
    }

    private static AssetRuntimeSyncService CreateService(TemporaryProjectFixture fixture, TestAssetApi api, out AssetRegistry registry)
        => CreateService(fixture, api, out registry, out _);

    private static AssetRuntimeSyncService CreateService(TemporaryProjectFixture fixture, TestAssetApi api, out AssetRegistry registry, out TestLogger<AssetRuntimeSyncService> logger)
    {
        registry = new AssetRegistry(new TestLogger<AssetRegistry>());
        registry.RegisterNewAssets(new[]
        {
            new AssetInfo(new AssetMeta("material"), fixture.Directory.GetPath("Project", "surface.MAT"))
        });
        logger = new TestLogger<AssetRuntimeSyncService>();
        return new AssetRuntimeSyncService(api, registry, logger);
    }
}
