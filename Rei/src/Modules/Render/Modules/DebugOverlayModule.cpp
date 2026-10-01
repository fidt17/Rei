#include "pch.h"
#include "Common/Profiling/ProfileMarkers.h"
#include <algorithm>
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
    void DebugOverlayModule::RenderProfiler() const
    {
        if (!ImGui::CollapsingHeader("CPU profiling")) return;
        auto& profiler = GetProfiler();
        const auto capture = profiler.CopySnapshot(profiling::SnapshotView::LastCapture);
        const bool capturing = capture.State == profiling::CaptureState::Queued || capture.State == profiling::CaptureState::Recording;
        const bool showCapture = capture.CaptureId && (capturing || !capture.ContinuousEnabled);
        const auto snapshot = showCapture ? capture : profiler.CopySnapshot();
        bool continuous = snapshot.ContinuousEnabled;
        if (ImGui::Checkbox("Continuous (120-frame windows)", &continuous)) profiler.SetEnabled(continuous);
        if (ImGui::Button("Capture 120 frames")) profiler.RequestCapture(120);
        ImGui::SameLine();
        if (ImGui::Button("Dump to log")) profiler.RequestLogDump();
        const char* state = "idle";
        switch (capture.State)
        {
            case profiling::CaptureState::Queued: state = "queued"; break;
            case profiling::CaptureState::Recording: state = "recording"; break;
            case profiling::CaptureState::Complete: state = "complete"; break;
            case profiling::CaptureState::Cancelled: state = "cancelled"; break;
            default: break;
        }
        ImGui::Text("Capture: %s | %u/%u frames", state, capture.FrameCount, capture.TargetFrames);
        ImGui::Text("Showing: %s", showCapture ? "capture" : "continuous window");
        ImGui::Text("CPU wall: %.2f ms avg, %.2f ms max", snapshot.FrameCount ? snapshot.DurationNs / 1e6 / snapshot.FrameCount : 0, snapshot.MaxFrameNs / 1e6);
        if (snapshot.InvalidFrames) ImGui::Text("Incomplete: %u invalid frames", snapshot.InvalidFrames);
        std::array<const profiling::Metric*, profiling::MAX_METRICS> scopes = {};
        u32 count = 0;
        const auto frames = snapshot.SampleFrames;
        for (u32 index = 0; index < snapshot.MetricCount; ++index)
        {
            const auto& metric = snapshot.Metrics[index];
            if (metric.Kind == profiling::MetricKind::Scope && metric.Calls) scopes[count++] = &metric;
            if (metric.Id == profiling::markers::DRAW_CALLS.Id) ImGui::Text("Engine draws/frame: %.1f", frames ? static_cast<f64>(metric.Value) / frames : 0);
            if (metric.Id == profiling::markers::TRIANGLES.Id) ImGui::Text("Submitted triangles/frame: %.0f", frames ? static_cast<f64>(metric.Value) / frames : 0);
        }
        std::sort(scopes.begin(), scopes.begin() + count, [](const auto* left, const auto* right) { return left->ExclusiveNs > right->ExclusiveNs; });
        if (ImGui::BeginTable("Profile scopes", 4))
        {
            ImGui::TableSetupColumn("Scope");
            ImGui::TableSetupColumn("Calls/frame");
            ImGui::TableSetupColumn("Incl ms/frame");
            ImGui::TableSetupColumn("Excl ms/frame");
            ImGui::TableHeadersRow();
            for (u32 index = 0; index < (std::min)(count, 8u); ++index)
            {
                const auto& metric = *scopes[index];
                ImGui::TableNextRow();
                ImGui::TableNextColumn(); ImGui::TextUnformatted(metric.Name.data());
                ImGui::TableNextColumn(); ImGui::Text("%.1f", frames ? static_cast<f64>(metric.Calls) / frames : 0);
                ImGui::TableNextColumn(); ImGui::Text("%.3f", frames ? metric.InclusiveNs / 1e6 / frames : 0);
                ImGui::TableNextColumn(); ImGui::Text("%.3f", frames ? metric.ExclusiveNs / 1e6 / frames : 0);
            }
            ImGui::EndTable();
        }
        ImGui::TextUnformatted("CPU wall-clock; engine draws exclude ImGui/direct project GL.");
    }

    void DebugOverlayModule::Setup(GLFWwindow* target)
    {
        _target = target;
        if (_target == nullptr) return;
        if (_isInitialized) return;

        IMGUI_CHECKVERSION();
        ImGui::CreateContext();
        ImGui::StyleColorsDark();

        // Chain Rei input callbacks; backend restores them during Dispose.
        ImGui_ImplGlfw_InitForOpenGL(_target, true);
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
        GetDiagnostics().SetDiagnosticsTime(0);
        if (!_isInitialized) return;
        if (!GetDiagnostics().IsDebugOverlayEnabled()) return;
        REI_PROFILE_SCOPE(profiling::markers::OVERLAY.Id);

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
        
        ImGui::Text("FPS: %d", static_cast<i32>(diagnostics.Fps + 0.5f));
        ImGui::Text("Frame wall: %.2f ms", diagnostics.FrameTimeMs);
        ImGui::Text("Measured sections: %.2f ms", diagnostics.MeasuredSectionsTimeMs);
        ImGui::Text("Core: %.2f ms", diagnostics.CoreTimeMs);
        ImGui::Text("Render: %.2f ms", diagnostics.RenderTimeMs);
        ImGui::Text("Swap Buffers: %.2f ms", diagnostics.PresentTimeMs);
        ImGui::Text("Diagnostics: %.2f ms", diagnostics.DiagnosticsTimeMs);
        ImGui::NewLine();
        ImGui::Text("Working Set: %.2f MB", diagnostics.WorkingSetMemoryMb);
        ImGui::Text("Private Memory: %.2f MB", diagnostics.PrivateMemoryMb);
        ImGui::Text("Loaded Asset Memory: %.2f MB", diagnostics.LoadedAssetsMemoryMb);
        ImGui::Text("Loaded Assets: %d", diagnostics.LoadedAssetCount);
        
        RenderProfiler();
        ImGui::End();

        ImGui::Render();
        ImGui_ImplOpenGL3_RenderDrawData(ImGui::GetDrawData());

        diagnosticsStopwatch.Stop();
        GetDiagnostics().SetDiagnosticsTime(diagnosticsStopwatch.ElapsedMs());
    }
}
