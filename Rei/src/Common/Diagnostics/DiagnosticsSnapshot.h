#pragma once

namespace rei::common::diagnostics
{
    struct DiagnosticsSnapshot
    {
        float Fps = 0.0f;
        float FrameTimeMs = 0.0f;
        float CoreTimeMs = 0.0f;
        float RenderTimeMs = 0.0f;
        float DiagnosticsTimeMs = 0.0f;

        float WorkingSetMemoryMb = 0.0f;
        float PrivateMemoryMb = 0.0f;

        i32 LoadedAssetCount = 0;
        float LoadedAssetsMemoryMb = 0.0f;
    };
}
