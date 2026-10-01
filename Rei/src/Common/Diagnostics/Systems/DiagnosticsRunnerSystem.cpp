#include "pch.h"
#include "Common/Profiling/ProfileMarkers.h"
#include "DiagnosticsRunnerSystem.h"

#include "Common/Diagnostics/DiagnosticsService.h"
#include "Engine/Services.h"

namespace rei::common::diagnostics
{
    void DiagnosticsRunnerSystem::OnUpdate()
    {
        REI_PROFILE_SCOPE(profiling::markers::DIAGNOSTICS.Id);
        auto& diagnostics = GetDiagnostics();
        diagnostics.Update();
        diagnostics.SetLoadedAssets(GetAssetManager().GetLoadedAssetCount(), GetAssetManager().GetLoadedAssetsSize());
    }
}
