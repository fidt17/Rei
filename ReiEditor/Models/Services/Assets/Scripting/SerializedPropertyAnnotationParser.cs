using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace ReiEditor.Models.Services.Assets.Scripting;

internal static class SerializedPropertyAnnotationParser
{
    internal readonly record struct Declaration(int Index, int EndIndex, string? Header, bool HideInEditor);

    // Literals are atomic: macro names and semicolons inside them are not declarations.
    internal const string LITERAL_PATTERN =
        @"R""(?<delimiter>[^()\s\\]{0,16})\([\s\S]*?\)\k<delimiter>""" +
        @"|""(?:\\[\s\S]|[^""\\])*""" +
        @"|\b[0-9](?:[\w.]|'(?=\w))*" +
        @"|(?:u8|u|U|L)?'(?:\\[\s\S]|[^'\\])*'";

    private const string TOKEN_PATTERN = LITERAL_PATTERN + @"|[A-Za-z_][A-Za-z_0-9]*|[^\s]";

    public static IEnumerable<Declaration> Parse(string text)
    {
        var tokens = Regex.Matches(text, TOKEN_PATTERN);
        string? header = null;
        var hideIndex = -1;
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (token.Value == SourceFileMacrosConstants.REI_HEADER)
            {
                if (header != null) throw new FormatException($"Duplicate REI_HEADER at offset {token.Index}.");
                if (index + 3 >= tokens.Count || tokens[index + 1].Value != "(" ||
                    !tokens[index + 2].Value.StartsWith('"') || tokens[index + 3].Value != ")")
                    throw new FormatException($"REI_HEADER expects one quoted string at offset {token.Index}.");

                header = JsonConvert.DeserializeObject<string>(tokens[index + 2].Value);
                if (string.IsNullOrWhiteSpace(header)) throw new FormatException($"REI_HEADER cannot be empty at offset {token.Index}.");
                index += 3;
                continue;
            }

            if (token.Value == SourceFileMacrosConstants.HIDE_IN_EDITOR)
            {
                hideIndex = token.Index;
                continue;
            }
            if (token.Value == SourceFileMacrosConstants.SERIALIZE)
            {
                var end = index + 1;
                while (end < tokens.Count && tokens[end].Value != ";") end++;
                if (end == tokens.Count) throw new FormatException($"Missing semicolon after SERIALIZE at offset {token.Index}.");
                var hideInEditor = hideIndex >= text.LastIndexOf('\n', token.Index) + 1;
                for (var attributeIndex = index + 1; attributeIndex < end; attributeIndex++)
                    hideInEditor |= tokens[attributeIndex].Value == SourceFileMacrosConstants.HIDE_IN_EDITOR;
                yield return new Declaration(token.Index, tokens[end].Index, header, hideInEditor);
                header = null;
                hideIndex = -1;
                index = end;
                continue;
            }

            if (header != null) throw new FormatException($"REI_HEADER must immediately precede a SERIALIZE declaration at offset {token.Index}.");
            hideIndex = -1;
        }

        if (header != null) throw new FormatException("REI_HEADER has no following SERIALIZE declaration.");
    }
}
