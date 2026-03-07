#include "pch.h"
#include "DebugOverlayToggleSystem.h"

#include "Common/Diagnostics/DiagnosticsService.h"
#include "Engine/Services.h"
#include "Modules/Input/Input.h"
#include "GLFW/glfw3.h"

namespace rei::render
{
    void DebugOverlayToggleSystem::OnUpdate()
    {
        if (!Input::IsKeyPressed(GLFW_KEY_F4)) return;

        GetDiagnostics().ToggleDebugOverlay();
    }
}
