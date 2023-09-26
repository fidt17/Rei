#include "pch.h"
#include "Engine.h"
#include <thread>
#include <chrono>

#include "Modules/UpdateLoop/UpdateLoopModule.h"
#include "Modules/UpdateLoop/Components/UpdateCallback.h"

namespace rei::internal::engine
{
    SET_LOG_SCOPE("ENGINE")

    void ConfigureAppUpdateCallback(const std::shared_ptr<ecs::World>& world, const std::shared_ptr<App>& app)
    {
        ECS_WORLD(*world);
        const auto appEntity = NEW_ENTITY();
        GET(appEntity, update_loop::UpdateCallback) = {[&]
        {
            app->OnUpdate();
        }};
    }

    Engine::Engine(std::shared_ptr<App> app)
        :
        _mainThread(main_thread::ReiMainThread()),
        _app(std::move(app)),
        _ecsWorld(std::make_shared<ecs::World>())
    {
        common::logging::Log::Initialize();

        LOG("Create engine")
        
        _mainThread.AddOnUpdateCallback(std::make_shared<std::function<void()>>([this]{ OnUpdate(); }));

        _ecsWorld->AddModule(std::make_shared<update_loop::UpdateLoopModule>());
    }

    void Engine::Start()
    {
        LOG("Run")

        ConfigureAppUpdateCallback(_ecsWorld, _app);

        _app->OnStart();
        _mainThread.Run();
    }

    void Engine::Shutdown(const int exitCode)
    {
        LOG("Shutdown. Exit code: " + std::to_string(exitCode))

        _app->OnShutdown();
        _mainThread.Stop();
    }

    void Engine::OnUpdate() const
    {
        _ecsWorld->Run();
    }
}
