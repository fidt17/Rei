#include "pch.h"
#include "Engine.h"

#include "Services.h"
#include "Modules/Assets/AssetManager.h"
#include "Modules/Behaviour/BehaviourModule.h"
#include "Modules/Components/EntityInfo.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "Modules/Scenes/SceneManager.h"
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
        GET(appEntity, update_loop::UpdateCallback) = {
            [&]
            {
                app->OnUpdate();
            }
        };
    }

    Engine::Engine(std::shared_ptr<App> app)
        :
        _mainThread(main_thread::ReiMainThread()),
        _app(std::move(app)),
        _internalWorld(std::make_shared<ecs::World>()),
        _assetManager(std::make_shared<assets::AssetManager>(R"(C:\Repos\Rei Projects\New Project\bin\Resources)")), // todo: from configuration ?
        _sceneManager(std::make_shared<scenes::SceneManager>()),
        _entityManager(std::make_shared<EntityManager>(_internalWorld))
    {
        Services::GetInstance()->SetInternalWorld(_internalWorld.get());
        Services::GetInstance()->SetEntityManager(_entityManager.get());

        _mainThread.AddOnUpdateCallback(std::make_shared<std::function<void()>>([this] { OnUpdate(); }));

        _internalWorld->AddModule(std::make_shared<update_loop::UpdateLoopModule>());
        _internalWorld->AddModule(std::make_shared<BehaviourModule>());
    }

    void Engine::Start()
    {
        try
        {
            _sceneManager->LoadScene(0);
            ConfigureAppUpdateCallback(_internalWorld, _app);
            _app->OnStart();
        }
        catch (const std::exception& e)
        {
            LOG_ERROR("Exception on engine start", e.what())
            Shutdown(-1);
        }

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
        _internalWorld->Run();
    }
}
