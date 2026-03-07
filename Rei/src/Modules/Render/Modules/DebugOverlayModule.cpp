#include "pch.h"
#include "DebugOverlayModule.h"

#include "Engine/Services.h"
#include "Common/Diagnostics/DiagnosticsService.h"
#include "Common/Time/Stopwatch.h"
#include "imgui.h"
#include "backends/imgui_impl_glfw.h"
#include "backends/imgui_impl_opengl3.h"
#include "GLFW/glfw3.h"

namespace rei::render
{
    void DebugOverlayModule::Setup(GLFWwindow* target)
    {
        _target = target;
        if (_target == nullptr) return;
        if (_isInitialized) return;

        IMGUI_CHECKVERSION();
        ImGui::CreateContext();
        ImGui::StyleColorsDark();

        ImGui_ImplGlfw_InitForOpenGL(_target, false);
        ImGui_ImplOpenGL3_Init("#version 330");
        _isInitialized = true;
    }

    void DebugOverlayModule::Dispose()
    {
        if (!_isInitialized) return;

        ImGui_ImplOpenGL3_Shutdown();
        ImGui_ImplGlfw_Shutdown();
        ImGui::DestroyContext();

        _isInitialized = false;
        _target = nullptr;
    }

    void DebugOverlayModule::Render()
    {
        if (!_isInitialized) return;
        if (!GetDiagnostics().IsDebugOverlayEnabled()) return;

        time::Stopwatch diagnosticsStopwatch;
        diagnosticsStopwatch.Start();
        const auto& diagnostics = GetDiagnostics().GetSnapshot();

        ImGui_ImplOpenGL3_NewFrame();
        ImGui_ImplGlfw_NewFrame();
        ImGui::NewFrame();

        ImGui::SetNextWindowPos(ImVec2(12.0f, 12.0f), ImGuiCond_Once);
        ImGui::SetNextWindowBgAlpha(0.85f);
        constexpr ImGuiWindowFlags WINDOW_FLAGS = ImGuiWindowFlags_NoResize | ImGuiWindowFlags_AlwaysAutoResize;
        ImGui::Begin("Diagnostics", nullptr, WINDOW_FLAGS);
        
        ImGui::Text("FPS: %d", static_cast<int>(diagnostics.Fps + 0.5f));
        ImGui::Text("Frame: %.2f ms", diagnostics.FrameTimeMs);
        ImGui::Text("Core: %.2f ms", diagnostics.CoreTimeMs);
        ImGui::Text("Render: %.2f ms", diagnostics.RenderTimeMs);
        ImGui::Text("Swap Buffers: %.2f ms", diagnostics.PresentTimeMs);
        ImGui::Text("Diagnostics: %.2f ms", diagnostics.DiagnosticsTimeMs);
        ImGui::NewLine();
        ImGui::Text("Working Set: %.2f MB", diagnostics.WorkingSetMemoryMb);
        ImGui::Text("Private Memory: %.2f MB", diagnostics.PrivateMemoryMb);
        ImGui::Text("Loaded Asset Memory: %.2f MB", diagnostics.LoadedAssetsMemoryMb);
        ImGui::Text("Loaded Assets: %d", diagnostics.LoadedAssetCount);
        
        ImGui::End();

        ImGui::Render();
        ImGui_ImplOpenGL3_RenderDrawData(ImGui::GetDrawData());

        diagnosticsStopwatch.Stop();
        GetDiagnostics().SetDiagnosticsTime(diagnosticsStopwatch.ElapsedMs());
    }
}
