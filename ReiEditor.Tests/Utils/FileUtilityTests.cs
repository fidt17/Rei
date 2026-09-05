using System.Globalization;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Utils;
using FileExtensions = ReiEditor.Models.Services.FileSystem.FileExtensions;

namespace ReiEditor.Tests.Utils;

/// <summary>Verifies file size display at unit boundaries and in the current culture.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Files")]
public sealed class FileSizeFormatterTests
{
    /// <summary>Sizes clamp negative input and transition through binary units without overflowing large values.</summary>
    [Theory]
    [InlineData(-1L, "0 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(1048576L, "1 MB")]
    [InlineData(1073741824L, "1 GB")]
    [InlineData(long.MaxValue, "8589934592 GB")]
    public void SizeDisplayUsesBinaryUnitBoundaries(long bytes, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.Equal(expected, FileSizeFormatter.FormatBytes(bytes));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>Fractional size display follows the caller's culture without changing global defaults.</summary>
    [Fact]
    public void SizeDisplayUsesCurrentDecimalSeparator()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            Assert.Equal("1,5 KB", FileSizeFormatter.FormatBytes(1536));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}

/// <summary>Verifies file discovery and size inspection against a real isolated directory tree.</summary>
[Trait("Category", "FileSystem")]
[Trait("Area", "Files")]
public sealed class FileInspectionTests
{
    /// <summary>Discovery expands directories recursively, removes duplicate paths, and ignores missing or blank inputs.</summary>
    [Fact]
    public void DiscoveryDeduplicatesFilesAndExpandsNestedDirectories()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(directory.GetPath("nested"));
        var first = directory.GetPath("one.png");
        var second = directory.GetPath("nested", "two.mat");
        File.WriteAllBytes(first, new byte[] { 1, 2, 3 });
        File.WriteAllText(second, "");

        var found = FileExtensions.FindAllFilesIn(new[] { directory.RootPath, first, first.ToUpperInvariant(), "", directory.GetPath("missing") });

        Assert.Equal(2, found.Count);
        Assert.Contains(first, found);
        Assert.Contains(second, found);
        Assert.Equal(3L, AssetFileInfoUtility.TryGetFileSize(first));
        Assert.Equal(0L, AssetFileInfoUtility.TryGetFileSize(second));
        Assert.Null(AssetFileInfoUtility.TryGetFileSize(directory.RootPath));
        Assert.Null(AssetFileInfoUtility.TryGetFileSize(directory.GetPath("missing")));
        Assert.Null(AssetFileInfoUtility.TryGetFileSize(" "));
    }
}
