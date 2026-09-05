using ReiEditor.Models.Services.Assets.Import;
using ReiEditor.Models.Services.FileSystem;

namespace ReiEditor.Tests.Models.Services.FileSystem;

/// <summary>Verifies that generated files stay hidden and genuine project assets remain visible.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "AssetFiltering")]
public sealed class AssetFileFilterTests
{
    /// <summary>Metadata and Visual Studio files must remain hidden regardless of filename casing.</summary>
    [Theory]
    [InlineData("image.png.meta", true)]
    [InlineData("image.png.META", true)]
    [InlineData("Game.vcxproj", true)]
    [InlineData("Game.VCXPROJ", true)]
    [InlineData("Game.VCXPROJ.USER", true)]
    [InlineData("image.png", false)]
    [InlineData("shader.rshader", false)]
    [InlineData("image.png.MeTa", true)]
    [InlineData("Game.VcxProj", true)]
    [InlineData("Game.VcxProj.User", true)]
    [InlineData("image.meta.png", false)]
    [InlineData("Game.vcxproj.backup", false)]
    [InlineData("Game.vcxproj.user.backup", false)]
    [InlineData("NoExtension", false)]
    [InlineData("directory.meta/image.png", false)]
    public void FileVisibilityRecognizesGeneratedExtensions(string fileName, bool hidden)
    {
        Assert.Equal(hidden, AssetFileFilter.ShouldHide(Path.Combine(Path.GetTempPath(), "ReiFilters", fileName)));
    }

    /// <summary>Only the actual Project/Scripts generated directories are excluded, not a similarly named parent segment.</summary>
    [Theory]
    [InlineData("Project/Scripts/bin", true)]
    [InlineData("PROJECT/SCRIPTS/CRASH_REPORTS/", true)]
    [InlineData("Project/Scripts/bin-old", false)]
    [InlineData("OtherProject/Scripts/bin", false)]
    [InlineData("Project/Textures", false)]
    [InlineData("MyProject/Scripts/bin", false)]
    [InlineData("OtherProject/Scripts/crash_reports", false)]
    [InlineData("Project/Scripts2/bin", false)]
    [InlineData("Project/Scripts/crash_reports-old", false)]
    [InlineData("Project\\Scripts\\BiN\\", true)]
    [InlineData("Project/Scripts/./bin/", true)]
    [InlineData("Project/Scripts/bin/../assets", false)]
    public void DirectoryVisibilityRespectsSegmentBoundaries(string relativePath, bool hidden)
    {
        Assert.Equal(hidden, AssetFileFilter.ShouldHideDirectory(Path.Combine(Path.GetTempPath(), "ReiFilters", relativePath)));
    }

    /// <summary>Blank inputs have no visible project entry and cannot be imported as assets.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void BlankInputsAreRejected(string input)
    {
        Assert.True(AssetFileFilter.ShouldHide(input));
        Assert.True(AssetFileFilter.ShouldHideDirectory(input));
        Assert.False(AssetImportUtils.IsValidAssetExtensionForMetaFile(input));
    }

    /// <summary>Generated metadata, C++ implementations, and project files must never acquire another meta file.</summary>
    [Theory]
    [InlineData(".meta", false)]
    [InlineData(".META", false)]
    [InlineData(".cpp", false)]
    [InlineData(".CPP", false)]
    [InlineData(".vcxproj", false)]
    [InlineData(".VCXPROJ", false)]
    [InlineData(".h", true)]
    [InlineData(".png", true)]
    [InlineData(".MeTa", false)]
    [InlineData(".Cpp", false)]
    [InlineData(".VcxProj", false)]
    [InlineData(".H", true)]
    [InlineData(".PNG", true)]
    [InlineData(".RSHADER", true)]
    [InlineData(".metadata", true)]
    [InlineData(".cppx", true)]
    public void MetaEligibilityIgnoresExtensionCasing(string extension, bool eligible)
    {
        Assert.Equal(eligible, AssetImportUtils.IsValidAssetExtensionForMetaFile(extension));
    }

    /// <summary>Extension matching and text editor support handle uppercase filenames and reject unsupported types.</summary>
    [Theory]
    [InlineData("code.CPP", true)]
    [InlineData("behaviour.h", true)]
    [InlineData("shader.RSHADER", true)]
    [InlineData("picture.png", false)]
    [InlineData("", false)]
    public void TextEditorSupportUsesKnownExtensions(string path, bool supported)
    {
        Assert.Equal(supported, FileExtensions.IsTextEditorOpenSupported(path));
        Assert.Equal(supported, FileExtensions.HasAnyExtension(path, new[] { ".cpp", ".h", ".rshader" }));
    }
}
