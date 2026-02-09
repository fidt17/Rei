using System.Collections.Generic;

namespace ReiEditor.Utils.Common;

public static class NamingUtils
{
    public static string GetUniqueName(string baseName, IEnumerable<string> existingNames)
    {
        var existing = new HashSet<string>(existingNames);
        if (!existing.Contains(baseName)) return baseName;

        var index = 1;
        string candidate;
        do
        {
            candidate = $"{baseName} {index}";
            index++;
        } while (existing.Contains(candidate));

        return candidate;
    }

    public static string GetDuplicateName(string baseName, IEnumerable<string> existingNames)
    {
        return GetUniqueName($"{baseName} Copy", existingNames);
    }
}
