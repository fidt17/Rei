using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Logging;
using ReiEditor.Tests.Infrastructure.Fixtures;

namespace ReiEditor.Tests.Models.Resources;

/// <summary>
/// Verifies resource persistence using real files inside an isolated temporary project.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Resources")]
public sealed class ResourceServiceTests
{
    /// <summary>
    /// Writing a resource creates missing parent directories and preserves JSON data for subsequent loading.
    /// </summary>
    [Fact]
    public async Task WriteCreatesNestedDirectoryAndLoadReadsStoredJson()
    {
        using var fixture = new TemporaryProjectFixture();
        var source = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "TestData", "Resources", "settings.json"));
        var path = fixture.Directory.GetPath("Project", "Settings", "scene.json");

        var written = await fixture.Resources.Write(source, path);
        var loaded = await fixture.Resources.Load<JObject>(path);

        Assert.True(written);
        Assert.True(fixture.Resources.Exists(path));
        Assert.Equal(source, await File.ReadAllTextAsync(path));
        Assert.Equal("Test scene", loaded.Value<string>("name"));
        Assert.Equal(3, loaded.Value<int>("entityCount"));
        Assert.DoesNotContain(fixture.ResourceLogger.Entries, entry => entry.Level == LogLevelEnum.Error);
    }
}
