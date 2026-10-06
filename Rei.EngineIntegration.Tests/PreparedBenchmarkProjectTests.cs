using System.Text.Json;

namespace Rei.EngineIntegration.Tests;

public sealed class PreparedBenchmarkProjectTests
{
    [Fact]
    public async Task UnchangedSourceReusesOwnedProjectAndRetainsBuiltAssetsAfterDispose()
    {
        await using var owner = new EngineIntegrationHarness(keepBuildOutputs: true);
        var source = CreateSource(owner.RunDirectory);
        var index = Path.Combine(owner.RunDirectory, "index");
        EngineIntegrationHarness? first = null;
        try
        {
            var original = await ExternalProjectCopy.FingerprintAsync(source);
            first = await PreparedBenchmarkProject.OpenAsync(source, indexDirectory: index);
            await first.PrepareProjectAsync(Path.Combine(owner.RunDirectory, "engine.rei_engine"), "msbuild.exe");
            var sourceManifest = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(Path.Combine(source, "bin", "Resources", "Cache", "asset-cache.json")));
            var copiedManifest = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(Path.Combine(first.ProjectDirectory, "bin", "Resources", "Cache", "asset-cache.json")));
            Assert.Equal(Path.Combine(first.ProjectDirectory, "asset.txt"), copiedManifest.GetProperty("Entries").GetProperty("1").GetProperty("AssetPath").GetString());
            foreach (var field in new[] { "AssetId", "ContentHash", "CacheFileName", "CacheSize" })
                Assert.Equal(sourceManifest.GetProperty("Entries").GetProperty("1").GetProperty(field).GetRawText(), copiedManifest.GetProperty("Entries").GetProperty("1").GetProperty(field).GetRawText());
            Assert.Equal(sourceManifest.GetProperty("CacheKey").GetString(), copiedManifest.GetProperty("CacheKey").GetString());
            Assert.Equal(sourceManifest.GetProperty("Entries").GetProperty("external").GetRawText(), copiedManifest.GetProperty("Entries").GetProperty("external").GetRawText());
            var cache = Path.Combine(first.ProjectDirectory, "bin", "Resources", "Cache", "asset.cache");
            Directory.CreateDirectory(Path.GetDirectoryName(cache)!);
            await File.WriteAllTextAsync(cache, "converted asset");
            await first.DisposeAsync();
            await using var second = await PreparedBenchmarkProject.OpenAsync(source, indexDirectory: index);
            Assert.Equal(first.RunDirectory, second.RunDirectory);
            Assert.Equal("converted asset", await File.ReadAllTextAsync(cache));
            Assert.Equal(original.ToArray(), (await ExternalProjectCopy.FingerprintAsync(source)).ToArray());
        }
        finally
        {
            if (first != null) Directory.Delete(first.RunDirectory, recursive: true);
            Directory.Delete(owner.RunDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SourceChangesCreateNewOwnedCopyWithoutDestroyingPreviousCache()
    {
        await using var owner = new EngineIntegrationHarness(keepBuildOutputs: true);
        var source = CreateSource(owner.RunDirectory);
        var index = Path.Combine(owner.RunDirectory, "index");
        EngineIntegrationHarness? first = null;
        EngineIntegrationHarness? second = null;
        try
        {
            first = await PreparedBenchmarkProject.OpenAsync(source, indexDirectory: index);
            await first.PrepareProjectAsync(Path.Combine(owner.RunDirectory, "engine.rei_engine"), "msbuild.exe");
            await File.WriteAllTextAsync(Path.Combine(source, "asset.txt"), "changed source");
            second = await PreparedBenchmarkProject.OpenAsync(source, indexDirectory: index);
            Assert.NotEqual(first.RunDirectory, second.RunDirectory);
            await second.PrepareProjectAsync(Path.Combine(owner.RunDirectory, "engine.rei_engine"), "msbuild.exe");
            Assert.Equal("changed source", await File.ReadAllTextAsync(Path.Combine(second.ProjectDirectory, "asset.txt")));
            Assert.True(Directory.Exists(first.ProjectDirectory));
        }
        finally
        {
            if (first != null) Directory.Delete(first.RunDirectory, recursive: true);
            if (second != null) Directory.Delete(second.RunDirectory, recursive: true);
            Directory.Delete(owner.RunDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task BuildOutputsAndEditTimestampReuseCacheButSceneSettingInvalidatesIt()
    {
        await using var owner = new EngineIntegrationHarness(keepBuildOutputs: true);
        var source = CreateSource(owner.RunDirectory);
        var index = Path.Combine(owner.RunDirectory, "index");
        var owned = new HashSet<string>();
        try
        {
            await using var first = await PreparedBenchmarkProject.OpenAsync(source, indexDirectory: index);
            owned.Add(first.RunDirectory);
            await first.PrepareProjectAsync(Path.Combine(owner.RunDirectory, "engine.rei_engine"), "msbuild.exe");
            await File.WriteAllTextAsync(Path.Combine(source, "bin", "Resources", "Cache", "retained.cache"), "rebuilt output");
            var projectFile = Path.Combine(source, "Fixture.rei");
            var project = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(projectFile))!;
            project["LastEditTime"] = "later timestamp";
            await File.WriteAllTextAsync(projectFile, project.ToJsonString());
            await using var second = await PreparedBenchmarkProject.OpenAsync(source, indexDirectory: index);
            Assert.Equal(first.RunDirectory, second.RunDirectory);
            project["LastSceneId"] = "another scene";
            await File.WriteAllTextAsync(projectFile, project.ToJsonString());
            await using var third = await PreparedBenchmarkProject.OpenAsync(source, indexDirectory: index);
            owned.Add(third.RunDirectory);
            Assert.NotEqual(first.RunDirectory, third.RunDirectory);
        }
        finally
        {
            foreach (var directory in owned) if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            Directory.Delete(owner.RunDirectory, recursive: true);
        }
    }

    private static string CreateSource(string root)
    {
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Fixture.rei"), JsonSerializer.Serialize(new
        {
            ProjectSolutionPath = "Fixture.sln", ProjectVisualStudioProjectPath = "Fixture.vcxproj"
        }));
        File.WriteAllText(Path.Combine(source, "Fixture.sln"), "solution");
        File.WriteAllText(Path.Combine(source, "Fixture.vcxproj"), "<Project><PropertyGroup><OutDir>$(SolutionDir)bin/$(Platform)$(Configuration)/$(ProjectName)/</OutDir><IntDir>$(SolutionDir)bin/int/</IntDir></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(source, "asset.txt"), "original source");
        var cache = Path.Combine(source, "bin", "Resources", "Cache");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "asset-cache.json"), JsonSerializer.Serialize(new
        {
            CacheKey = "version|1", Entries = new Dictionary<string, object>
            {
                ["1"] = new { AssetId = "1", AssetPath = Path.Combine(source, "asset.txt"), ContentHash = "retained content hash", CacheFileName = "retained.cache", CacheSize = 6 },
                ["external"] = new { AssetPath = Path.Combine(root, "shared", "engine.asset") }
            }
        }));
        File.WriteAllText(Path.Combine(cache, "retained.cache"), "binary");
        return source;
    }
}
