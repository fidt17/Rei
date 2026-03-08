namespace ReiEditor.Utils;

public static class FileSizeFormatter
{
    public static string FormatBytes(long bytes)
    {
        var safeBytes = bytes < 0 ? 0 : bytes;
        var suffixes = new[] { "B", "KB", "MB", "GB" };
        var suffixIndex = 0;
        double readable = safeBytes;

        while (readable >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            readable /= 1024;
            suffixIndex++;
        }

        return $"{readable:0.##} {suffixes[suffixIndex]}";
    }
}
