#pragma once

#include "App.h"
#include "Engine/Engine.h"
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
            // todo: pass via params
            const bool IS_EDITOR = true;
            
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
        const auto engine = rei::external::CreateEngine(std::filesystem::current_path().string().c_str(), rei::internal::engine::EngineMode::PlayMode);

        WindowCreationSettings windowSettings;
        windowSettings.Name = "Main Window";
        windowSettings.Width = 1080;
        windowSettings.Height = 720;
        windowSettings.HideOnCreation = false;
        windowSettings.CenterCursor = true;
        windowSettings.HideCursor = false;
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
