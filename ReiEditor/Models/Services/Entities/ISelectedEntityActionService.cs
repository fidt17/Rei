using System;

namespace ReiEditor.Models.Services.Entities;

public interface ISelectedEntityActionService
{
    event Action<int>? RenameEntityRequested;

    bool DeleteSelectedEntity();
    bool DuplicateSelectedEntity();
    bool RequestRenameSelectedEntity();
}
