using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ReiEditor.Models.Services.Render;

namespace ReiEditor.Models.Services.Assets.Shaders;

public class ShaderUniformParser : IShaderUniformParser
{
    private static readonly Regex UniformRegex = new(
        @"uniform\s+(?:(?:lowp|mediump|highp)\s+)?(?<type>[A-Za-z0-9_]+)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*(?:\[[^\]]+\])?)\s*(?:=\s*[^;]+)?;",
        RegexOptions.Compiled);

    private static readonly Regex CommentsRegex = new(
        @"((\/[*])([\s\S]+?)([*]\/))|([/]{2,}[^\n]+)",
        RegexOptions.Compiled);

    public IReadOnlyList<ShaderUniformInfo> ParseUniforms(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return Array.Empty<ShaderUniformInfo>();

        var noComments = CommentsRegex.Replace(source, "");
        var matches = UniformRegex.Matches(noComments);
        if (matches.Count == 0) return Array.Empty<ShaderUniformInfo>();

        var uniforms = new List<ShaderUniformInfo>();
        foreach (Match match in matches)
        {
            var sourceType = match.Groups["type"].Value.Trim();
            var name = match.Groups["name"].Value.Trim();
            if (string.IsNullOrWhiteSpace(sourceType) || string.IsNullOrWhiteSpace(name)) continue;

            uniforms.Add(new ShaderUniformInfo(name, sourceType, MapType(sourceType)));
        }

        return uniforms;
    }

    private static ShaderUniformType MapType(string sourceType)
    {
        if (string.Equals(sourceType, "float", StringComparison.OrdinalIgnoreCase))
        {
            return ShaderUniformType.Float;
        }

        if (string.Equals(sourceType, "int", StringComparison.OrdinalIgnoreCase))
        {
            return ShaderUniformType.Integer;
        }

        if (string.Equals(sourceType, "vec4", StringComparison.OrdinalIgnoreCase))
        {
            return ShaderUniformType.Color;
        }

        if (string.Equals(sourceType, "sampler2D", StringComparison.OrdinalIgnoreCase))
        {
            return ShaderUniformType.Texture;
        }

        return ShaderUniformType.Unsupported;
    }
}
