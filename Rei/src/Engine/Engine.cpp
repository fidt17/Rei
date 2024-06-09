#include "pch.h"
#include "Engine.h"

#include <utility>

#include "Services.h"
#include "Ecs/DeleteHere.h"
#include "Modules/Assets/AssetManager.h"
#include "Modules/Behaviour/Components/StartBehavioursEvent.h"
#include "Modules/Behaviour/Systems/StartBehavioursSystem.h"
#include "Modules/Behaviour/Systems/UpdateBehavioursSystem.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "Modules/Scenes/SceneManager.h"
#include "Startup/App.h"

namespace rei::internal::engine
{
    SET_LOG_SCOPE("ENGINE")

    Engine::Engine(std::shared_ptr<App> app)
        :
        _reiMainThread(std::make_shared<TaskExecutor>()),
        _renderer(std::make_shared<render::Renderer>()),
        _app(std::move(app)),
        _internalWorld(std::make_shared<ecs::World>()),
        _assetManager(std::make_shared<assets::AssetManager>(R"(C:\Repos\Rei Projects\New Project\bin\Resources)")), // todo: from configuration ?
        _entityManager(std::make_shared<EntityManager>(_internalWorld)),
        _sceneManager(std::make_shared<scenes::SceneManager>(_entityManager))
    {
        Services::GetInstance()->SetEngine(this);
        Services::GetInstance()->SetInternalWorld(_internalWorld.get());
        Services::GetInstance()->SetEntityManager(_entityManager.get());
        Services::GetInstance()->SetRenderer(_renderer.get());

        ConfigureInternalWorld();
    }

    void Engine::ConfigureInternalWorld()
    {
        _internalWorld->AddSystem([&] { _reiMainThread->CompleteTasks(); });
        _internalWorld->AddSystem([&] { _windowManager.OnUpdate(); });

        _internalWorld->AddSystem<StartBehavioursSystem>();
        _internalWorld->AddSystem<DeleteHere<StartBehavioursEvent>>();
        _internalWorld->AddSystem<UpdateBehavioursSystem>();

        _internalWorld->AddSystem([&] { _app->OnUpdate(); });
        _internalWorld->AddSystem([&] { _renderer->Render(); });
    }

    std::shared_ptr<window::Window> Engine::CreateMainWindow()
    {
        auto mainWindow = _mainWindowHandler.CreateMainWindow(_windowManager);
        _mainWindowHandler.MainWindowClosedEvent.append([&]
        {
            LOG("Main window was closed")
            _renderer->SetTarget(nullptr);
            Shutdown(MAIN_WINDOW_CLOSED_EXIT_CODE);
        });

        _renderer->SetTarget(mainWindow->GetGLFWWindow());

        return mainWindow;
    }

    void Engine::Start()
    {
        try
        {
            _runEngine = true;

            _sceneManager->LoadScene(0);
            _app->OnStart();
        }
        catch (const std::exception& exc)
        {
            LOG_ERROR("Exception on engine start", exc.what())
            Shutdown(ENGINE_INITIALIZATION_ERROR_EXIT_CODE);
        }

        RunUpdateLoop();
    }

    void Engine::RunUpdateLoop()
    {
        try
        {
            while (_runEngine)
            {
                _internalWorld->Run();
            }
        }
        catch (const std::exception& exc)
        {
            LOG_ERROR("Exception in engine update loop", exc.what())
            Shutdown(ENGINE_UPDATE_ERROR_EXIT_CODE);
        }
    }

    void Engine::Shutdown(const int exitCode)
    {
        if (!_runEngine) return;

        LOG("Engine shutdown")

        _exitCode = exitCode;
        _runEngine = false;

        _app->OnShutdown();
        _windowManager.Dispose();

        LOG("Shutdown complete")
    }

    int Engine::GetExitCode() const
    {
        return _exitCode;
    }

    TaskExecutor& Engine::GetMainThread() const
    {
        return *_reiMainThread;
    }
}
