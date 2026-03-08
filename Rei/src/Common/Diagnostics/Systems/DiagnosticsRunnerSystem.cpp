#include "pch.h"
#include "DiagnosticsRunnerSystem.h"

#include "Common/Diagnostics/DiagnosticsService.h"
#include "Engine/Services.h"

namespace rei::common::diagnostics
{
    void DiagnosticsRunnerSystem::OnUpdate()
    {
        auto& diagnostics = GetDiagnostics();
        diagnostics.Update();
        diagnostics.SetLoadedAssets(GetAssetManager().GetLoadedAssetCount(), GetAssetManager().GetLoadedAssetsSize());
    }
}
