#include "pch.h"
#include "Engine.h"

#include <utility>

#include "Services.h"
#include "Ecs/Systems/DeleteHere.h"
#include "Modules/Assets/AssetManager.h"
#include "Modules/Behaviour/Components/StartBehavioursEvent.h"
#include "Modules/Behaviour/Systems/StartBehavioursSystem.h"
#include "Modules/Behaviour/Systems/UpdateBehavioursSystem.h"
#include "Modules/Editor/FlyCameraSystem.h"
#include "Modules/Editor/SelectEntityWithCursorSystem.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "Modules/Input/Input.h"
#include "Modules/Render/Camera/AssignMainCameraSystem.h"
#include "Modules/Scenes/SceneManager.h"
#include "Startup/App.h"

namespace rei::internal::engine
{
    SET_LOG_SCOPE("ENGINE")

    Engine::Engine(std::shared_ptr<App> app, const EngineMode mode) :
        _mode(mode),
        _windowManager(std::make_shared<window::WindowManager>()),
        _mainWindowHandler(std::make_shared<window::MainWindowHandler>()),
        _reiMainThread(std::make_shared<TaskExecutor>()),
        _mainRenderer(std::make_shared<render::Renderer>()),
        _app(std::move(app)),
        _internalWorld(std::make_shared<ecs::World>()),
        _assetManager(std::make_shared<assets::AssetManager>()),
        _entityManager(std::make_shared<EntityManager>(_internalWorld)),
        _sceneManager(std::make_shared<scenes::SceneManager>(_assetManager, _entityManager))
    {
        Services::GetInstance()->SetEngine(this);
        Services::GetInstance()->SetAssetManager(_assetManager);
        Services::GetInstance()->SetInternalWorld(_internalWorld);
        Services::GetInstance()->SetEntityManager(_entityManager);
        Services::GetInstance()->SetWindowManager(_windowManager);

        ConfigureInternalWorld();
        SetupGLFW();
    }

    void Engine::SetupGLFW() const
    {
        glfwSetErrorCallback([](int error_code, const char* description)
        {
            LOG_ERROR("GLFW ERROR. " + STRING(error_code) + " " + description);
        });

        if (!glfwInit())
        {
            REI_THROW("GLFW Initialization error")
        }

        glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 3);
        glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 3);
        glfwWindowHint(GLFW_SAMPLES, 4);
        glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);
    }

    void Engine::ConfigureInternalWorld() const
    {
        _internalWorld->AddSystem([&] { _windowManager->OnUpdate(); });

        if (IsPlaymode())
        {
            _internalWorld->AddSystem<behaviour::StartBehavioursSystem>(_entityManager);
            _internalWorld->AddSystem<ecs::DeleteHere<StartBehavioursEvent>>();

            _internalWorld->AddSystem<behaviour::UpdateBehavioursSystem>(_entityManager);
            _internalWorld->AddSystem([&] { _app->OnUpdate(); });
        }

        _internalWorld->AddSystem<render::AssignMainCameraSystem>(_mainRenderer);
        _internalWorld->AddSystem<editor::FlyCameraSystem>();
        
        _internalWorld->AddSystem<editor::SelectEntityWithCursorSystem>();

        _internalWorld->AddSystem([&] { _mainRenderer->Render(); });

        _internalWorld->AddSystem([&] { _reiMainThread->CompleteTasks(); });
    }

    std::shared_ptr<window::Window> Engine::CreateMainWindow(const WindowCreationSettings& settings)
    {
        auto mainWindow = _mainWindowHandler->CreateMainWindow(*_windowManager, settings);
        _mainWindowHandler->MainWindowClosedEvent.append([&]
        {
            LOG("Main window was closed")
            _mainRenderer->SetTarget(nullptr);
            ExecuteOnMainThread([&]
            {
                Shutdown(MAIN_WINDOW_CLOSED_EXIT_CODE);
            });
        });

        mainWindow->SizeChangedEvent.append([&](const int width, const int height)
        {
            if (_mainRenderer->GetCamera().IsNull()) return;

            _mainRenderer->GetCamera().Get().SetOutputSize(width, height);
        });

        Input::SetSource(mainWindow->GetGLFWWindow());

        _mainRenderer->SetTarget(mainWindow->GetGLFWWindow());

        return mainWindow;
    }

    void Engine::Start()
    {
        try
        {
            while (!_mainWindowHandler->IsSet())
            {
                _reiMainThread->CompleteTasks();
            }

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
        _mainRenderer->Dispose();
        glfwTerminate();
        Services::GetInstance()->GetAssetManager().DeleteTmpFiles();

        LOG("Shutdown complete")
        ShutdownEvent(_exitCode);
    }

    bool Engine::IsPlaymode() const
    {
        return _mode == PlayMode;
    }

    bool Engine::IsEditor() const
    {
        return _mode == EditorMode;
    }

    int Engine::GetExitCode() const
    {
        return _exitCode;
    }

    std::shared_ptr<Task> Engine::ExecuteOnMainThread(std::function<void()> fn) const
    {
        auto t = std::make_shared<Task>(fn);
        _reiMainThread->AddTask(t);
        return t;
    }
}
