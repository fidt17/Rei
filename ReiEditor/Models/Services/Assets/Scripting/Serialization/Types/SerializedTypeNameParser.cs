using System;

namespace ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;

public static class SerializedTypeNameParser
{
    public static string NormalizeSourceType(string type)
    {
        var trimmedType = type.Trim();
        var templateStartIndex = trimmedType.IndexOf('<');
        if (templateStartIndex == -1)
        {
            return GetBaseTypeName(trimmedType);
        }

        var templateEndIndex = FindMatchingTemplateEnd(trimmedType, templateStartIndex);
        if (templateEndIndex == -1)
        {
            return GetBaseTypeName(trimmedType);
        }

        var outerType = trimmedType.Substring(0, templateStartIndex).Trim();
        var innerType = trimmedType.Substring(templateStartIndex + 1, templateEndIndex - templateStartIndex - 1).Trim();
        return $"{GetBaseTypeName(outerType)}<{NormalizeSourceType(innerType)}>";
    }

    public static string GetBaseTypeName(string type)
    {
        var trimmedType = type.Trim();
        var templateStartIndex = trimmedType.IndexOf('<');
        if (templateStartIndex != -1)
        {
            trimmedType = trimmedType[..templateStartIndex];
        }

        var namespaceSeparatorIndex = trimmedType.LastIndexOf("::", StringComparison.Ordinal);
        if (namespaceSeparatorIndex == -1) return trimmedType.Trim();

        return trimmedType[(namespaceSeparatorIndex + 2)..].Trim();
    }

    public static string? GetTemplateTypeName(string type)
    {
        var trimmedType = type.Trim();
        var templateStartIndex = trimmedType.IndexOf('<');
        if (templateStartIndex == -1) return null;

        var templateEndIndex = FindMatchingTemplateEnd(trimmedType, templateStartIndex);
        if (templateEndIndex == -1) return null;

        var templateType = trimmedType.Substring(templateStartIndex + 1, templateEndIndex - templateStartIndex - 1).Trim();
        return NormalizeSourceType(templateType);
    }

    private static int FindMatchingTemplateEnd(string value, int templateStartIndex)
    {
        var depth = 0;

        for (var index = templateStartIndex; index < value.Length; index++)
        {
            if (value[index] == '<')
            {
                depth++;
            }
            else if (value[index] == '>')
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }
}
