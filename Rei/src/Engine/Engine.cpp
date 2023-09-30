#include "pch.h"
#include "Engine.h"
#include <thread>
#include <chrono>

#include "Services.h"
#include "Modules/UpdateLoop/UpdateLoopModule.h"
#include "Modules/UpdateLoop/Components/UpdateCallback.h"
#include "Startup/App.h"

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
        _ecsWorld(std::make_shared<ecs::World>()),
        _assetManager(std::make_shared<assets::AssetManager>("C:\\Repos\\Rei Projects\\New Project\\bin\\Resources"))
    {
        common::logging::Log::Initialize();

        LOG("Create engine")

        Services::GetInstance()->SetAssetManager(_assetManager);
        
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
