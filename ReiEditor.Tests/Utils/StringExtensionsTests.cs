using System.Xml;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Tests.Utils;

/// <summary>Verifies ordinal substring searches and XML value extraction.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Strings")]
public sealed class StringExtensionsTests
{
    /// <summary>Substring search returns non-overlapping positions and does not match different casing.</summary>
    [Fact]
    public void IndexSearchIsOrdinalAndNonOverlapping()
    {
        Assert.Equal(new[] { 0, 2 }, "aaaaa".AllIndexesOf("aa"));
        Assert.Equal(new[] { 0, 8 }, "Cat cat Cat".AllIndexesOf("Cat"));
        Assert.Empty("".AllIndexesOf("x"));
        Assert.Empty("cat".AllIndexesOf("DOG"));
    }

    /// <summary>Empty or null search text is rejected instead of entering a non-progressing search loop.</summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void IndexSearchRejectsEmptyPattern(string? pattern)
    {
        Assert.Throws<ArgumentException>(() => "source".AllIndexesOf(pattern!));
    }

    /// <summary>XML extraction returns the first matching decoded value, null for missing elements, and rejects malformed XML.</summary>
    [Fact]
    public void XmlValueExtractionPreservesContentAndReportsInvalidXml()
    {
        const string XML = "<root><name>A &amp; B</name><name>second</name></root>";
        Assert.Equal("A & B", StringExtensions.GetValueFromXml(XML, "name"));
        Assert.Null(StringExtensions.GetValueFromXml(XML, "missing"));
        Assert.Throws<XmlException>(() => StringExtensions.GetValueFromXml("<root>", "name"));
    }
}
