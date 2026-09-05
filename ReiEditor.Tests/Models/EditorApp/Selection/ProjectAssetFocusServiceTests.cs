using ReiEditor.Models.EditorApp.Selection;

namespace ReiEditor.Tests.Models.EditorApp.Selection;

/// <summary>
/// Verifies project asset focus request validation and payload preservation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Selection")]
public sealed class ProjectAssetFocusServiceTests
{
    /// <summary>
    /// Blank asset IDs and paths do not publish focus requests.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TestBlankFocusValuesAreIgnored(string? value)
    {
        var service = new ProjectAssetFocusService();
        var assetRequests = new List<string>();
        var pathRequests = new List<string>();
        service.FocusAssetRequested += assetRequests.Add;
        service.FocusAssetPathRequested += pathRequests.Add;

        service.FocusAsset(value!);
        service.FocusAssetPath(value!);

        Assert.Empty(assetRequests);
        Assert.Empty(pathRequests);
    }

    /// <summary>
    /// Nonblank focus values are published without trimming or normalization.
    /// </summary>
    [Fact]
    public void TestFocusRequestsPreservePayloads()
    {
        var service = new ProjectAssetFocusService();
        string? assetId = null;
        string? assetPath = null;
        service.FocusAssetRequested += value => assetId = value;
        service.FocusAssetPathRequested += value => assetPath = value;

        service.FocusAsset("  material-id  ");
        service.FocusAssetPath("  Assets/Materials/Test.mat  ");

        Assert.Equal("  material-id  ", assetId);
        Assert.Equal("  Assets/Materials/Test.mat  ", assetPath);
    }
}
