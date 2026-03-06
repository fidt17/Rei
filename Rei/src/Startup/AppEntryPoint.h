#pragma once

#include "App.h"
#include "Engine/Engine.h"
#include "Common/Diagnostics/CrashReporter.h"
#include "Modules/EntityManagement/EntityManager.h"

#ifdef REI_APP

namespace rei
{
    class BehaviourRegistry;
}

extern std::shared_ptr<rei::App> CreateApp();
extern void ConfigureComponentsFactory(rei::BehaviourRegistry& factory);

namespace rei::external
{
    REI_EXTERN_API inline internal::engine::Engine* CreateEngine(const char* resourcesDir, const i32 mode)
    {
        try
        {
            rei::common::diagnostics::CrashReporter::Initialize(resourcesDir, "ReiEngine");

            #ifdef REI_EDITOR
            const bool IS_EDITOR = true;
            #else
            const bool IS_EDITOR = false;
            #endif
            
            std::filesystem::current_path(resourcesDir);
            auto engine = new internal::engine::Engine(CreateApp(), static_cast<internal::engine::EngineMode>(mode), IS_EDITOR);
            ConfigureComponentsFactory(GetEntityManager().GetBehaviourRegistry());
            return engine;
        }
        catch (const std::exception& e)
        {
            LOG_ERROR("Exception {}", e.what())
            return nullptr;
        }
    }

    REI_EXTERN_API inline void Start(internal::engine::Engine* engine)
    {
        engine->Start();
    }

    REI_EXTERN_API inline int Shutdown(internal::engine::Engine* engine, const int exitCode)
    {
        if (engine == nullptr) return -1;

        if (engine->IsRunning())
        {
            engine->ExecuteOnMainThread([engine, exitCode]
            {
                engine->Shutdown(exitCode);
            })->WaitForCompletion();
        }
        else
        {
            engine->Shutdown(exitCode);
        }

        return 0;
    }

    REI_EXTERN_API inline void DestroyEngine(internal::engine::Engine* engine)
    {
        if (engine == nullptr) return;
        delete engine;
    }
}

int main()
{
    try
    {
        const auto initialWorkingDirectory = std::filesystem::current_path();
        rei::common::diagnostics::CrashReporter::Initialize(initialWorkingDirectory, "ReiApp");
        const auto projectName = initialWorkingDirectory.filename().string();
        const auto engine = rei::external::CreateEngine(initialWorkingDirectory.string().c_str(), rei::internal::engine::EngineMode::PlayMode);

        WindowCreationSettings windowSettings;
        windowSettings.Name = projectName.empty() ? "Rei App" : projectName;
        windowSettings.Width = 1080;
        windowSettings.Height = 720;
        windowSettings.HideOnCreation = false;
        windowSettings.CenterCursor = true;
        windowSettings.HideCursor = false;
        #ifdef REI_EDITOR
        windowSettings.FullScreen = false;
        #else
        windowSettings.FullScreen = true;
        #endif
        engine->CreateMainWindow(windowSettings);
        
        rei::external::Start(engine);
        return engine->GetExitCode();
    }
    catch (const std::exception& e)
    {
        LOG_ERROR("Exception {}", e.what())
        return DEFAULT_ERROR_EXIT_CODE;
    }
}

#else

class BlankApp final : public rei::App
{
public:
    void OnStart() override
    {
    }

    void OnUpdate() override
    {
    }

    void OnShutdown() override
    {
    }
};

int main()
{
    try
    {
        rei::common::diagnostics::CrashReporter::Initialize(std::filesystem::current_path(), "ReiApp");
        auto engine = new rei::internal::engine::Engine(std::make_shared<BlankApp>(), rei::internal::engine::EngineMode::PlayMode, false);
        engine->Start();
        return engine->GetExitCode();
    }
    catch (const std::exception& e)
    {
        LOG_ERROR("Exception", e.what())
        return DEFAULT_ERROR_EXIT_CODE;
    }
}

#endif
