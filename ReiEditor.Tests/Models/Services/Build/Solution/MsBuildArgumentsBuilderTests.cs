using ReiEditor.Models.Services.Build;
using ReiEditor.Models.Services.Build.Solution;

namespace ReiEditor.Tests.Models.Services.Build.Solution;

/// <summary>Verifies MSBuild command argument composition.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Build")]
public sealed class MsBuildArgumentsBuilderTests
{
    /// <summary>Required arguments quote solution paths and preserve requested configuration and target.</summary>
    [Fact]
    public void TestBuildCreatesRequiredArguments()
    {
        var result = MsBuildArgumentsBuilder.Build(@"C:\Project With Spaces\Game.sln", BuildConfigurationEnum.Release, "Rebuild");

        Assert.Equal("\"C:\\Project With Spaces\\Game.sln\" -v:q /t:Rebuild /p:Configuration=Release", result);
    }

    /// <summary>Output path is absolute, quoted, stripped of existing separators, and ends with escaped separator pair.</summary>
    [Fact]
    public void TestBuildNormalizesAndQuotesOutputDirectory()
    {
        var output = Path.Combine(Path.GetTempPath(), "Rei Output With Spaces") + Path.DirectorySeparatorChar;

        var result = MsBuildArgumentsBuilder.Build("Game.sln", BuildConfigurationEnum.Debug, "Build", output);

        var expectedOutput = Path.GetFullPath(output).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar + Path.DirectorySeparatorChar;
        Assert.EndsWith($" /p:OutDir=\"{expectedOutput}\"", result);
    }

    /// <summary>Null, empty, and whitespace output values omit OutDir.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TestBuildOmitsBlankOutputDirectory(string? output)
    {
        var result = MsBuildArgumentsBuilder.Build("Game.sln", BuildConfigurationEnum.EditorDebug, "Clean", output);

        Assert.DoesNotContain("OutDir", result);
    }
}
