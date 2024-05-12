#include "pch.h"
#include "Engine.h"

#include "Services.h"
#include "Modules/Assets/AssetManager.h"
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
        _app(std::move(app)),
        _internalWorld(std::make_shared<ecs::World>()),
        _assetManager(std::make_shared<assets::AssetManager>(R"(C:\Repos\Rei Projects\New Project\bin\Resources)")), // todo: from configuration ?
        _entityManager(std::make_shared<EntityManager>(_internalWorld)),
        _sceneManager(std::make_shared<scenes::SceneManager>(_entityManager))
    {
        Services::GetInstance()->SetInternalWorld(_internalWorld.get());
        Services::GetInstance()->SetEntityManager(_entityManager.get());
        Services::GetInstance()->SetRenderer(&_renderer);

        _internalWorld->AddModule(std::make_shared<update_loop::UpdateLoopModule>());
    }

    void Engine::Start()
    {
        try
        {
            _runEngine = true;
            _renderer.SetupWindow(400, 400, "Main Window");

            _sceneManager->LoadScene(0);
            ConfigureAppUpdateCallback(_internalWorld, _app);
            _app->OnStart();
        }
        catch (const std::exception& exc)
        {
            LOG_ERROR("Exception on engine start", exc.what())
            Shutdown(-1);
        }

        RunUpdateLoop();
    }

    void Engine::RunUpdateLoop()
    {
        try
        {
            while (_runEngine)
            {
                _renderer.Render();
                _internalWorld->Run();
            }
        }
        catch (const std::exception& exc)
        {
            LOG_ERROR("Exception in engine update loop", exc.what())
            Shutdown(-2);
        }
    }

    void Engine::Shutdown(const int exitCode)
    {
        LOG("Shutdown. Exit code: " + std::to_string(exitCode))

        _runEngine = false;

        _app->OnShutdown();
        _renderer.Terminate();
    }
}
