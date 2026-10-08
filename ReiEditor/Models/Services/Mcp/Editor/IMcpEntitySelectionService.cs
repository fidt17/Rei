using System.Collections.Generic;
using ReiEditor.Mcp.Contracts;

namespace ReiEditor.Models.Services.Mcp.Editor;

internal interface IMcpEntitySelectionService
{
    ReiSelectionState Read(string source);
    ReiSelectionState Set(IReadOnlyList<int> entityIds);
}
