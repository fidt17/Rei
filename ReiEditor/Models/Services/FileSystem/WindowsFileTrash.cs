using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ReiEditor.Models.Services.FileSystem;

public sealed class WindowsFileTrash : IFileTrash
{
    private const uint DELETE = 0x0003;
    private const ushort NO_CONFIRMATION = 0x0010;
    private const ushort ALLOW_UNDO = 0x0040;
    private const ushort NO_CONNECTED_ELEMENTS = 0x2000;
    private const ushort WARN_BEFORE_PERMANENT_DELETE = 0x4000;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileOperation
    {
        public nint Owner;
        public uint Function;
        public nint Source;
        public nint Destination;
        public ushort Flags;
        public int Aborted;
        public nint NameMappings;
        public nint ProgressTitle;
    }

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHFileOperationW(ref FileOperation operation);

    public void MoveToTrash(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return;
        if (!OperatingSystem.IsWindows() || !Environment.UserInteractive)
            throw new PlatformNotSupportedException("The Windows Recycle Bin requires an interactive Windows session.");
        if (paths.Any(path => !Path.IsPathFullyQualified(path) || path.IndexOfAny(['\0', '*', '?']) >= 0))
            throw new ArgumentException("Recycle Bin paths must be absolute paths without wildcards.", nameof(paths));

        // The Shell API reads a double-null terminated list of UTF-16 paths.
        var source = Marshal.StringToHGlobalUni(string.Join('\0', paths) + '\0');
        try
        {
            var operation = new FileOperation
            {
                Function = DELETE,
                Source = source,
                Flags = ALLOW_UNDO | NO_CONFIRMATION | NO_CONNECTED_ELEMENTS | WARN_BEFORE_PERMANENT_DELETE
            };

            var error = SHFileOperationW(ref operation);
            if (operation.Aborted != 0) throw new OperationCanceledException();
            if (error != 0) throw new IOException($"The Windows Recycle Bin operation failed (0x{error:X}).");
        }
        finally
        {
            Marshal.FreeHGlobal(source);
        }
    }
}
