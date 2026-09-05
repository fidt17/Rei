using ReiEditor.Models.Resources;
using ReiEditor.Tests.Infrastructure.Fixtures;

namespace ReiEditor.Tests.Models.Resources;

/// <summary>
/// Verifies direct text resource loading from isolated files.
/// </summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Resources")]
public sealed class ResourceUtilsTests
{
    /// <summary>
    /// Existing UTF-8 resource loads without altering text.
    /// </summary>
    [Fact]
    public async Task LoadReadsUtf8Text()
    {
        using var fixture = new TemporaryProjectFixture();
        var path = fixture.Directory.GetPath("localized.txt");
        const string CONTENT = "Rei — ресурс ✓";
        await File.WriteAllTextAsync(path, CONTENT);

        var loaded = await ResourceUtils.Load(path);

        Assert.Equal(CONTENT, loaded);
    }

    /// <summary>
    /// Missing resource throws error containing requested path.
    /// </summary>
    [Fact]
    public async Task LoadMissingResourceThrows()
    {
        using var fixture = new TemporaryProjectFixture();
        var path = fixture.Directory.GetPath("Project", "missing.json");

        var exception = await Assert.ThrowsAsync<Exception>(() => ResourceUtils.Load(path));

        Assert.Contains(path, exception.Message);
    }
}
