using System.Globalization;
using ReiEditor.Models.Resources;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Tests.Infrastructure.Fixtures;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Assets.Scripting;

[Trait("Area", "Assets")]
public sealed class PropertyRangeParsingTests
{
    private readonly SourceFilesUtility _parser = new(null!, null!, null!);

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void RangeHeaderAndHiddenMetadataFollowOneDeclaration(string newline)
    {
        var source = "REI_HEADER(\"Lighting\") /* comment */ REI_RANGE(0, 8)" + newline +
                     "HIDE_IN_EDITOR SERIALIZE i32 Count = 4;" + newline + "SERIALIZE f32 Plain = 1;";
        var properties = _parser.GetSerializedProperties(source);
        var count = properties["Count"];
        Assert.Equal("Lighting", count.HeaderBefore);
        Assert.True(count.HideInEditor);
        Assert.Equal(new SerializedNumericRange(0, 8, null, true), count.Range);
        Assert.Null(properties["Plain"].Range);
    }

    [Theory]
    [InlineData("REI_RANGE(-16.0f, 16.0F) REI_HEADER(\"Tone\") SERIALIZE f32 Value = 0;", -16, 16, null)]
    [InlineData("REI_RANGE(-1e-2f, +2e-2f, 1e-3f) SERIALIZE float Value = 0;", -0.01f, 0.02f, 0.001f)]
    [InlineData("REI_RANGE(.0, 1., .05) SERIALIZE f32 Value = 0.5f;", 0, 1, 0.05f)]
    [InlineData("REI_RANGE(-2147483648, 2147483647, 2) SERIALIZE i32 Value = 0;", int.MinValue, int.MaxValue, 2)]
    public void ParsesTypedBoundsAndOptionalStep(string source, double minimum, double maximum, object? step)
    {
        var range = _parser.GetSerializedProperties(source)["Value"].Range!;
        Assert.Equal(minimum, range.Minimum);
        Assert.Equal(maximum, range.Maximum);
        Assert.Equal(step == null ? null : (double?)Convert.ToDouble(step), range.Step);
    }

    [Fact]
    public void LiteralsAndCommentsDoNotCreateRangeDeclarations()
    {
        const string SOURCE = """
            const char* ignored = R"tag(REI_RANGE(0, 8) SERIALIZE i32 Ghost;)tag";
            // REI_RANGE(0, 8)
            SERIALIZE i32 Plain = 1;
            REI_RANGE(/* low */ 0, /* high */ 8) SERIALIZE int Value = 4;
            """;
        var properties = _parser.GetSerializedProperties(SOURCE);
        Assert.Equal(new[] { "Plain", "Value" }, properties.Keys);
        Assert.Null(properties["Plain"].Range);
        Assert.Equal(8, properties["Value"].Range!.Maximum);
    }

    [Theory]
    [InlineData("REI_RANGE() SERIALIZE i32 Value;")]
    [InlineData("REI_RANGE(0) SERIALIZE i32 Value;")]
    [InlineData("REI_RANGE(0, 8, 1, 2) SERIALIZE i32 Value;")]
    [InlineData("REI_RANGE(8, 0) SERIALIZE i32 Value;")]
    [InlineData("REI_RANGE(0, 0) SERIALIZE i32 Value;")]
    [InlineData("REI_RANGE(0, 8, 0) SERIALIZE i32 Value;")]
    [InlineData("REI_RANGE(0, 8, -1) SERIALIZE i32 Value;")]
    [InlineData("REI_RANGE(0, 8, 9) SERIALIZE i32 Value;")]
    [InlineData("REI_RANGE(0, 8, .5) SERIALIZE i32 Value;")]
    [InlineData("REI_RANGE(0, 2147483648) SERIALIZE i32 Value;")]
    [InlineData("REI_RANGE(0.0, 8) SERIALIZE i32 Value;")]
    [InlineData("REI_RANGE(0, LIMIT) SERIALIZE i32 Value;")]
    [InlineData("REI_RANGE(0, 4 + 4) SERIALIZE i32 Value;")]
    [InlineData("REI_RANGE(0, std::max(4, 8)) SERIALIZE i32 Value;")]
    [InlineData("REI_RANGE(0, 8) REI_RANGE(0, 4) SERIALIZE i32 Value;")]
    [InlineData("REI_RANGE(0, 8) int Plain; SERIALIZE i32 Value;")]
    [InlineData("REI_RANGE(0, 8)")]
    [InlineData("REI_RANGE(0, 8 SERIALIZE i32 Value;")]
    [InlineData("REI_RANGE SERIALIZE i32 Value;")]
    [InlineData("SERIALIZE REI_RANGE(0, 8) i32 Value;")]
    [InlineData("REI_RANGE(0, 8) SERIALIZE u32 Value;")]
    [InlineData("REI_RANGE(0, 8) SERIALIZE double Value;")]
    [InlineData("REI_RANGE(0, 8) SERIALIZE bool Value;")]
    [InlineData("REI_RANGE(0, 8) SERIALIZE std::vector<i32> Value;")]
    [InlineData("REI_RANGE(0, 8) SERIALIZE Vector3 Value;")]
    [InlineData("REI_RANGE(0, 8) SERIALIZE i32 Value = 9;")]
    [InlineData("REI_RANGE(1, 8) SERIALIZE i32 Value;")]
    [InlineData("REI_RANGE(NaN, 8) SERIALIZE f32 Value;")]
    [InlineData("REI_RANGE(0, Infinity) SERIALIZE f32 Value;")]
    [InlineData("REI_RANGE(0, 1e100f) SERIALIZE f32 Value;")]
    [InlineData("REI_RANGE(1, 1.00000001) SERIALIZE f32 Value = 1;")]
    [InlineData("REI_RANGE(0, 1, 1e-100f) SERIALIZE f32 Value;")]
    public void RejectsInvalidMetadata(string source) => Assert.Throws<FormatException>(() => _parser.GetSerializedProperties(source));

    [Fact]
    public void FieldErrorsIncludeNameAndSourceType()
    {
        var error = Assert.Throws<FormatException>(() => _parser.GetSerializedProperties("REI_RANGE(0, 8) SERIALIZE double Count = 4;"));
        Assert.Contains("Count (double)", error.Message);
    }

    [Fact]
    public void SourceNumbersUseInvariantCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            Assert.Equal(0.5f, _parser.GetSerializedProperties("REI_RANGE(-0.5f, 0.5f) SERIALIZE f32 Value = 0.25f;")["Value"].Range!.Maximum);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public async Task RangeDoesNotChangeGeneratedNativeSerialization()
    {
        using var project = new TemporaryProjectFixture();
        SerializableObjectInfo Schema(string source) => new("game", "Config", false, new ObjectFile<string>(source, "Config.h"), _parser.GetSerializedProperties(source), "Config.h");
        var resources = new TestResourceService(project.Resources);
        var generator = new BehaviourRegistrySourceGenerator(resources, new SerializableObjectsRegistry(new TestLogger<SerializableObjectsRegistry>()));
        await generator.GenerateBehaviourRegistrySourceFile(new(), [Schema("SERIALIZE i32 Count = 4; SERIALIZE f32 Speed = 0.5f;")], []);
        await generator.GenerateBehaviourRegistrySourceFile(new(), [Schema("REI_RANGE(0, 8) SERIALIZE i32 Count = 4; REI_RANGE(0, 1, 0.05f) SERIALIZE f32 Speed = 0.5f;")], []);
        Assert.Equal(resources.Writes[0].Data, resources.Writes[1].Data);
    }
}