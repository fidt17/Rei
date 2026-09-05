using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Utils;

/// <summary>Verifies collision-free entity and duplicate names without filesystem access.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Naming")]
public sealed class NamingUtilsTests
{
    /// <summary>A free base name is preserved, while collisions use the first available numeric suffix.</summary>
    [Theory]
    [InlineData("Camera", "", "Camera")]
    [InlineData("Camera", "Camera|Camera 1|Camera 3", "Camera 2")]
    [InlineData("Camera", "camera", "Camera")]
    public void UniqueNameUsesFirstAvailableExactName(string name, string existing, string expected)
    {
        Assert.Equal(expected, NamingUtils.GetUniqueName(name, existing.Split('|', StringSplitOptions.RemoveEmptyEntries)));
    }

    /// <summary>Duplicate names use the Copy suffix and skip names already used by other duplicates.</summary>
    [Fact]
    public void DuplicateNameSkipsExistingCopies()
    {
        Assert.Equal("Camera Copy 2", NamingUtils.GetDuplicateName("Camera", new[] { "Camera Copy", "Camera Copy 1", "Camera Copy 3" }));
    }
}
