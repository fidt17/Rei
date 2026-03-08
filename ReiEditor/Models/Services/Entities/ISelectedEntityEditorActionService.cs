using System;

namespace ReiEditor.Models.Services.Entities;

public interface ISelectedEntityEditorActionService
{
    event Action<int>? RenameEntityRequested;

    bool DeleteSelectedEntity();
    bool DuplicateSelectedEntity();
    bool RequestRenameSelectedEntity();
}
