#pragma once
#include "AssetTestSupport.h"
#include "glad/glad.h"
#include "Engine/Engine.h"
#include "Modules/Assets/Core/AssetIds.h"
#include "Modules/Render/Shaders/ShaderGenerator.h"
#include <condition_variable>
#include <future>

namespace rei::tests
{
    inline std::vector<u8> TextBytes(const std::string& text)
    {
        std::vector<u8> bytes;
        AppendString(bytes, text);
        return bytes;
    }

    inline void PrepareEngineResources(TemporaryDirectory& files, const std::string& marker = "first-session", const nlohmann::json& entities = nlohmann::json::array())
    {
        // Small real shader programs, independent of the user's resource pack.
        const std::string shader =
            "\n#ifdef VERTEX\nlayout(location=0) in vec3 position;\nvoid main(){gl_Position=vec4(position,1);}\n#endif\n"
            "#ifdef FRAGMENT\nout vec4 result;\nvoid main(){result=vec4(1,0,1,1);}\n#endif\n";
        std::vector<PackedAsset> pack = {
            {"0", "build-scenes", JsonAsset({{"Scenes", {{"0", "empty-scene"}}}})},
            {"empty-scene", "empty", JsonAsset({{"Name", "native fixture"}, {"Entities", entities}})}
        };
        for (const auto id : {REI_SHADER_INCLUDE_AMBIENT_LIGHT_ASSET_ID, REI_SHADER_INCLUDE_POINT_LIGHT_ASSET_ID,
            REI_SHADER_INCLUDE_SHADER_COMMON_ASSET_ID, REI_SHADER_INCLUDE_VERTEX_COMMON_ASSET_ID, REI_SHADER_INCLUDE_FRAGMENT_COMMON_ASSET_ID})
            pack.push_back({id, id, TextBytes("// " + marker + "\n")});
        for (const auto id : {REI_SHADER_ERROR_ASSET_ID, REI_SHADER_LIGHT_SOURCE_ASSET_ID, REI_SHADER_ALPHA_OUTLINE_ASSET_ID,
            REI_SHADER_OVERLAY_TEXTURE_ASSET_ID, REI_SHADER_GRAYSCALE_ASSET_ID, REI_SHADER_INVERSION_ASSET_ID,
            REI_SHADER_DEPTH_ASSET_ID, REI_SHADER_COLOR_ASSET_ID, REI_SHADER_EDITOR_GRID_ASSET_ID, REI_SHADER_TEXT_ASSET_ID})
            pack.push_back({id, id, TextBytes(shader)});
        WriteAssetPack(files, pack);
    }

    class EngineProbeApp final : public App
    {
    public:
        std::atomic<i32> Starts = 0;
        std::atomic<i32> Updates = 0;
        std::atomic<i32> Shutdowns = 0;
        std::function<void()> StartAction;
        std::function<void()> UpdateAction;
        std::function<void()> ShutdownAction;
        std::function<void()> CreateModulesAction;
        std::vector<std::unique_ptr<render::CustomRenderModule>> CreateCustomRenderModules() override { if (CreateModulesAction) CreateModulesAction(); return {}; }
        void OnStart() override { ++Starts; if (StartAction) StartAction(); }
        void OnUpdate() override { ++Updates; if (UpdateAction) UpdateAction(); }
        void OnShutdown() override { ++Shutdowns; if (ShutdownAction) ShutdownAction(); }
    };

    // Shared C++ integration host: owned resources/scene, actual engine/render
    // thread, bounded readiness, native task reads, diagnostics in child report.
    // Editor-driven tests continue to use EngineIntegrationHarness.cs.
    class NativeEngineFixture
    {
        enum class ActionState { Pending, Running, Complete, Cancelled };
        struct ActionRequest
        {
            std::atomic<ActionState> State = ActionState::Pending;
            std::promise<void> Completion;
        };
        class EngineFinished final : public std::runtime_error
        {
        public:
            EngineFinished() : std::runtime_error("Native engine finished before queued fixture action") {}
        };
    public:
        BehaviourFixture Resources;
        std::shared_ptr<EngineProbeApp> App = std::make_shared<EngineProbeApp>();
        std::unique_ptr<internal::engine::Engine> Engine;
        std::function<void()> BeforeStartAction;

        explicit NativeEngineFixture(const internal::engine::EngineMode mode = internal::engine::PlayMode, const std::string& marker = "first-session", const std::function<void(TemporaryDirectory&)>& prepareResources = {})
            : Resources([&](TemporaryDirectory& files) { if (prepareResources) prepareResources(files); else PrepareEngineResources(files, marker); }),
              _mode(mode)
        {
        }

        ~NativeEngineFixture()
        {
            try { Stop(); } catch (...) {}
            Engine.reset();
            Services::GetInstance()->SetEngine(nullptr);
        }

        void Start(const u32 timeoutMs = 10000)
        {
            if (_thread.joinable() || Engine) throw std::runtime_error("Native fixture may only start once");
            _thread = std::thread([this]
            {
                try
                {
                    auto engine = std::make_unique<internal::engine::Engine>(App, _mode, false);
                    {
                        std::scoped_lock lock(_mutex);
                        Engine = std::move(engine);
                    }
                    if (!_stopRequested)
                    {
                        ConfigureComponentsFactory(GetEntityManager().GetBehaviourRegistry());
                        Engine->StartEvent.append([this]
                        {
                            {
                                std::scoped_lock lock(_mutex);
                                _ready = true;
                                _changed.notify_all();
                            }
                            // Close can precede Start's own run=true write. This
                            // worker-thread hook closes that startup race.
                            if (_stopRequested) Engine->Shutdown(0);
                        });
                        if (BeforeStartAction) BeforeStartAction();
                        if (!_stopRequested)
                        {
                            glfwWindowHint(GLFW_DECORATED, GLFW_FALSE);
                            const WindowCreationSettings settings{"Rei native integration", 32, 24, true, false, false};
                            Engine->CreateMainWindow(settings);
                            if (!_stopRequested) Engine->Start();
                        }
                    }
                }
                catch (...) { std::scoped_lock lock(_mutex); _error = std::current_exception(); }
                std::scoped_lock lock(_mutex);
                _finished = true;
                _changed.notify_all();
            });
            std::unique_lock lock(_mutex);
            const auto signaled = _changed.wait_for(lock, std::chrono::milliseconds(timeoutMs), [this] { return _ready || _finished; });
            if (!signaled) throw std::runtime_error("Native engine readiness timeout");
            if (_error) std::rethrow_exception(_error);
            if (!_ready)
            {
                std::string diagnostics = "Native engine exited before readiness";
                for (const auto& entry : common::logging::GetRecentLogEntriesSnapshot()) diagnostics += "\n" + entry;
                throw std::runtime_error(diagnostics);
            }
        }

        void OnEngineThread(const std::function<void()>& action, const u32 timeoutMs = 10000)
        {
            auto request = std::make_shared<ActionRequest>();
            auto finished = request->Completion.get_future();
            Engine->ExecuteOnMainThread([action, request]
            {
                auto pending = ActionState::Pending;
                if (!request->State.compare_exchange_strong(pending, ActionState::Running)) return;
                try { action(); request->Completion.set_value(); }
                catch (...) { request->Completion.set_exception(std::current_exception()); }
                request->State = ActionState::Complete;
            });
            const auto deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(timeoutMs);
            while (finished.wait_for(std::chrono::milliseconds(1)) != std::future_status::ready)
            {
                bool engineFinished;
                { std::scoped_lock lock(_mutex); engineFinished = _finished; }
                if (engineFinished || std::chrono::steady_clock::now() >= deadline)
                {
                    auto pending = ActionState::Pending;
                    if (request->State.compare_exchange_strong(pending, ActionState::Cancelled))
                    {
                        if (engineFinished) throw EngineFinished();
                        throw std::runtime_error("Native fixture action timeout");
                    }
                    // Running actions may use caller-owned references. Keep
                    // caller alive; outer owned Job bounds an irrecoverable hang.
                }
            }
            finished.get();
        }

        u64 WaitForNextFrames(const u32 frameCount = 2, const u32 timeoutMs = 10000)
        {
            // Completed native frames exist in both modes; App::OnUpdate does
            // not run in Editor mode and therefore cannot serve as frame clock.
            OnEngineThread([] { GetProfiler().SetEnabled(true); });
            const auto deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(timeoutMs);
            auto snapshot = GetProfiler().CopySnapshot();
            const auto wait = [&]
            {
                { std::scoped_lock lock(_mutex); if (_finished) throw EngineFinished(); }
                if (std::chrono::steady_clock::now() >= deadline) throw std::runtime_error("Native fixture frame timeout");
                std::this_thread::sleep_for(std::chrono::milliseconds(1));
                snapshot = GetProfiler().CopySnapshot();
            };
            while (snapshot.FrameCount == 0) wait();
            const auto first = snapshot.LastFrame;
            while (snapshot.LastFrame - first < frameCount) wait();
            return snapshot.LastFrame;
        }

        void Stop(const i32 exitCode = 0)
        {
            if (!_thread.joinable()) return;
            _stopRequested = true;
            std::exception_ptr error;
            internal::engine::Engine* engine;
            { std::scoped_lock lock(_mutex); engine = Engine.get(); }
            if (engine && engine->IsRunning())
            {
                try { OnEngineThread([this, exitCode] { Engine->Shutdown(exitCode); }); }
                catch (...) { error = std::current_exception(); }
            }
            _thread.join();
            if (_error) std::rethrow_exception(_error);
            if (error)
            {
                try { std::rethrow_exception(error); }
                catch (const EngineFinished&) { return; }
            }
        }

    private:
        internal::engine::EngineMode _mode;
        std::thread _thread;
        std::mutex _mutex;
        std::condition_variable _changed;
        bool _ready = false;
        bool _finished = false;
        std::exception_ptr _error;
        std::atomic<bool> _stopRequested = false;
    };
}
