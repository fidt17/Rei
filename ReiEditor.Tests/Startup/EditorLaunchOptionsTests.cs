using ReiEditor.Startup;

namespace ReiEditor.Tests.Startup;

public sealed class EditorLaunchOptionsTests
{
    [Fact]
    public void DefaultsToUserStorageAndNoStartupProject()
    {
        Assert.Equal(Path.Combine("documents", "Rei Engine"), EditorLaunchOptions.GetStorageDirectory(null, "documents"));
        Assert.Null(EditorLaunchOptions.LoadStartupProject(null));
    }

    [Fact]
    public void ExplicitStorageIsIsolatedAndRelativeOverridesAreRejected()
    {
        var isolated = Path.Combine(Path.GetTempPath(), "rei-test-storage");
        Assert.Equal(Path.GetFullPath(isolated), EditorLaunchOptions.GetStorageDirectory(isolated, "documents"));
        Assert.Throws<ArgumentException>(() => EditorLaunchOptions.GetStorageDirectory("relative", "documents"));
        Assert.Throws<ArgumentException>(() => EditorLaunchOptions.LoadStartupProject("relative.rei"));
    }
}
