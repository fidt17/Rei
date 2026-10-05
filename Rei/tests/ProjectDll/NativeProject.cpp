#include "pch.h"
#include "support/BehaviourTestProbes.h"
#include "NativeProjectJournal.h"
#include <mutex>
#include <chrono>
#include <thread>
#define REI_APP
#include "Startup/AppEntryPoint.h"

namespace
{
    struct JournalSink
    {
        rei::tests::ProjectJournalCallback Callback = nullptr;
        rei::tests::ProjectCancelCallback Cancel = nullptr;
        void* Context = nullptr;
        void Emit(const rei::tests::ProjectJournalEvent event) const { if (Callback) Callback(Context, static_cast<i32>(event)); }
        bool IsCancelled() const { return Cancel && Cancel(Context); }
    };

    std::mutex ConfigurationMutex;
    JournalSink ConfiguredSink;
    i32 FailureMode = 0;
    std::weak_ptr<rei::ecs::World> ProjectWorld;

    struct NativeWorkerException final : std::runtime_error
    {
        JournalSink Sink;
        explicit NativeWorkerException(const JournalSink sink) : std::runtime_error("Injected native worker failure after readiness"), Sink(sink) {}
        ~NativeWorkerException() override { Sink.Emit(rei::tests::ProjectJournalEvent::WorkerExceptionDestroyed); }
    };

    void WaitForCancellation(const JournalSink& sink)
    {
        const auto deadline = std::chrono::steady_clock::now() + std::chrono::seconds(5);
        while (!sink.IsCancelled())
        {
            if (std::chrono::steady_clock::now() >= deadline) throw std::runtime_error("Native project cancellation injection timeout");
            std::this_thread::sleep_for(std::chrono::milliseconds(1));
        }
    }

    struct NativeOwnedAsset
    {
        JournalSink Sink;
        explicit NativeOwnedAsset(const JournalSink sink) : Sink(sink) { Sink.Emit(rei::tests::ProjectJournalEvent::AssetCreated); }
        ~NativeOwnedAsset() { Sink.Emit(rei::tests::ProjectJournalEvent::AssetDestroyed); }
    };

    struct NativeCallbackToken
    {
        JournalSink Sink;
        explicit NativeCallbackToken(const JournalSink sink) : Sink(sink) { Sink.Emit(rei::tests::ProjectJournalEvent::CallbackCreated); }
        ~NativeCallbackToken() { Sink.Emit(rei::tests::ProjectJournalEvent::CallbackDestroyed); }
    };

    class NativeProjectApp final : public rei::App
    {
    public:
        JournalSink Sink;
        i32 Mode;
        NativeProjectApp(const JournalSink sink, const i32 mode) : Sink(sink), Mode(mode) { Sink.Emit(rei::tests::ProjectJournalEvent::AppCreated); }
        ~NativeProjectApp() override { Sink.Emit(rei::tests::ProjectJournalEvent::AppDestroyed); }

        void OnStart() override
        {
            ProjectWorld = rei::GetInternalWorld();
            // Actual Engine::Start has now set its run flag. A cancellation that
            // arrived before Start cannot be erased by that later store.
            if (Sink.IsCancelled()) { rei::GetEngine().Shutdown(75); return; }
            if (Mode == 2) throw std::runtime_error("Injected native project startup exit");
            auto asset = rei::GetAssetManager().CreateAssetWithId<rei::tests::ProbeDataAsset>("project-probe");
            asset.Get()->Number = NATIVE_PROJECT_VERSION;
            asset.Get()->Label = "project DLL";
            rei::GetAssetManager().CreateAssetWithId<NativeOwnedAsset>("project-owned-lifetime", Sink);
            rei::tests::CurrentTrace = std::make_shared<rei::tests::TraceState>();
            rei::tests::CurrentTrace->Action = [sink = Sink](const i32 id, const std::string& stage, rei::ecs::Entity)
            {
                if (id == rei::tests::PROBE_C && stage == "Dispose") sink.Emit(rei::tests::ProjectJournalEvent::BehaviourDisposed);
            };
            const auto entity = rei::GetEntityManager().CreateNewEntity("DLL lifecycle probe");
            rei::GetEntityManager().AddBehaviour(entity, rei::tests::PROBE_C, nlohmann::json(), false);
            Sink.Emit(rei::tests::ProjectJournalEvent::BehaviourCreated);
            auto token = std::make_shared<NativeCallbackToken>(Sink);
            rei::GetEngine().StartEvent.append([token] { token->Sink.Emit(rei::tests::ProjectJournalEvent::Ready); });
        }

        void OnShutdown() override { Sink.Emit(rei::tests::ProjectJournalEvent::AppShutdown); }
    };
}

std::shared_ptr<rei::App> CreateApp()
{
    std::scoped_lock lock(ConfigurationMutex);
    if (FailureMode == 1) throw std::runtime_error("Injected native project CreateApp failure");
    return std::make_shared<NativeProjectApp>(ConfiguredSink, FailureMode);
}

REI_EXTERN_API void NativeProjectConfigureJournal(rei::tests::ProjectJournalCallback callback, rei::tests::ProjectCancelCallback cancel, void* context, const i32 failureMode)
{
    std::scoped_lock lock(ConfigurationMutex);
    ConfiguredSink = {callback, cancel, context};
    FailureMode = failureMode;
}

REI_EXTERN_API bool NativeProjectWorldAlive() { return !ProjectWorld.expired(); }

REI_EXTERN_API void* NativeProjectCreateEngine(const char* resourcesDir, const i32 mode)
{
    JournalSink sink;
    i32 failureMode = 0;
    { std::scoped_lock lock(ConfigurationMutex); sink = ConfiguredSink; failureMode = FailureMode; }
    sink.Emit(rei::tests::ProjectJournalEvent::CreationEntered);
    if (failureMode == 3) WaitForCancellation(sink);
    auto* engine = rei::external::CreateEngine(resourcesDir, mode);
    if (engine) ProjectWorld = rei::GetInternalWorld();
    return engine;
}

REI_EXTERN_API void NativeProjectStart(void* value)
{
    auto* engine = static_cast<rei::internal::engine::Engine*>(value);
    JournalSink sink;
    i32 failureMode = 0;
    { std::scoped_lock lock(ConfigurationMutex); sink = ConfiguredSink; failureMode = FailureMode; }
    engine->StartEvent.append([sink, engine] { if (sink.IsCancelled()) engine->Shutdown(75); });
    sink.Emit(rei::tests::ProjectJournalEvent::StartEntered);
    if (failureMode == 5) WaitForCancellation(sink);
    rei::external::Start(engine);
    if (failureMode == 4)
    {
        WaitForCancellation(sink);
        throw NativeWorkerException(sink);
    }
}

REI_EXTERN_API bool NativeProjectCreateHiddenWindow(void* value)
{
    auto* engine = static_cast<rei::internal::engine::Engine*>(value);
    if (!engine) return false;
    ProjectWorld = rei::GetInternalWorld();
    const WindowCreationSettings settings{"Native project lifecycle", 32, 24, true, false, false};
    return engine->CreateMainWindow(settings) != nullptr;
}

REI_EXTERN_API void NativeProjectResetInstrumentation()
{
    rei::tests::CurrentTrace.reset();
    std::scoped_lock lock(ConfigurationMutex);
    ConfiguredSink = {};
    FailureMode = 0;
}

REI_EXTERN_API i32 NativeProjectVersion() { return NATIVE_PROJECT_VERSION; }

REI_EXTERN_API i32 NativeProjectRead()
{
    i32 result = -1;
    rei::GetEngine().ExecuteOnMainThread([&]
    {
        nlohmann::json data;
        if (rei::GetAssetManager().TryGetLoadedAssetData("project-probe", data)) result = data.at("Number").get<i32>();
    })->WaitForCompletion();
    return result;
}

REI_EXTERN_API i32 NativeProjectLoadSaved()
{
    i32 result = -1;
    rei::GetEngine().ExecuteOnMainThread([&]
    {
        auto asset = rei::GetAssetManager().GetById<rei::tests::ProbeDataAsset>("persisted-probe");
        if (asset.IsLoaded()) result = asset.Get()->Number;
    })->WaitForCompletion();
    return result;
}
