#include "pch.h"
#include "Engine.h"
#include <thread>
#include <chrono>

#include "Modules/UpdateLoop/Components/UpdateCallback.h"
#include "Modules/UpdateLoop/Systems/InvokeUpdateCallbackSystem.h"

namespace rei::internal::engine
{
    SET_LOG_SCOPE("ENGINE")
    
    Engine::Engine(std::shared_ptr<App> app) : _app(std::move(app)), _ecsWorld(ecs::World())
    {
        LOG("Create engine")

        _ecsWorld.AddSystem<update_loop::InvokeUpdateCallbackSystem>();
    }
    
    void Engine::Start()
    {
        LOG("Run")
        
        ECS_WORLD(_ecsWorld);
        const auto appEntity = NEW_ENTITY();
        GET(appEntity, update_loop::UpdateCallback) = { [&]{_app->OnUpdate();} };
        
        _ecsWorld.Refresh();
        _app->OnStart();

        _mainThread = std::thread([&]()
        {
            _mainThreadRunFlag = true;
            while (_mainThreadRunFlag)
            {
                try
                {
                    OnUpdate();
                }
                catch (const std::exception& e)
                {
                    LOG_ERROR("Exception in main thread", e.what())
                }

                std::this_thread::sleep_for(std::chrono::seconds(1));
            }
        });
    }

    void Engine::Shutdown(const int exitCode)
    {
        LOG("Shutdown. Exit code: " + std::to_string(exitCode))
        
        _app->OnShutdown();

        _mainThreadRunFlag = false;
        _mainThread.join();
    }

    void Engine::OnUpdate()
    {
        _ecsWorld.Run();
    }
}
