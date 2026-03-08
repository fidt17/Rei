#pragma once

namespace rei::common::diagnostics
{
    struct DiagnosticsSnapshot
    {
        f32 Fps = 0.0f;
        f32 FrameTimeMs = 0.0f;
        f32 CoreTimeMs = 0.0f;
        f32 RenderTimeMs = 0.0f;
        f32 PresentTimeMs = 0.0f;
        f32 DiagnosticsTimeMs = 0.0f;

        f32 WorkingSetMemoryMb = 0.0f;
        f32 PrivateMemoryMb = 0.0f;

        i32 LoadedAssetCount = 0;
        f32 LoadedAssetsMemoryMb = 0.0f;
    };
}
