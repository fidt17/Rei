using System.Collections.Generic;

namespace ReiEditor.Models.Services.FileSystem;

public interface IFileTrash
{
    void MoveToTrash(IReadOnlyList<string> paths);
}
