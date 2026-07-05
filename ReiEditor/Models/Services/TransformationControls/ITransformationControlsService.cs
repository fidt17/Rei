using System;

namespace ReiEditor.Models.Services.TransformationControls;

public interface ITransformationControlsService
{
    event Action? StateChanged;

    bool CanUseLocalSpace { get; }
    bool CanUseWorldSpace { get; }
    bool IsLocalSpace { get; }
    bool IsWorldSpace { get; }
    bool CanUseRectTransformMode { get; }
    bool EngineRunning { get; }
    TransformationMode Mode { get; }

    void SetWorldSpace();
    void SetLocalSpace();
    void SetMode(TransformationMode mode);
}
