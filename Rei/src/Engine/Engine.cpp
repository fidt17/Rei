#include "pch.h"
#include "Engine.h"

#include <utility>

#include "Services.h"
#include "Modules/Assets/Core/AssetManager.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "Modules/Input/Input.h"
#include "Modules/Render/Shaders/ShaderGenerator.h"
#include "Modules/Scenes/SceneManager.h"
#include "Startup/App.h"

namespace rei::internal::engine
{

    Engine::Engine(std::shared_ptr<App> app, const EngineMode mode, const bool isEditor) :
        _mode(mode),
        _isEditor(isEditor),
        _windowManager(std::make_shared<window::WindowManager>()),
        _mainWindowHandler(std::make_shared<window::MainWindowHandler>()),
        _mainThread(std::make_shared<TaskExecutor>()),
        _mainRenderer(std::make_shared<render::Renderer>()),
        _app(std::move(app)),
        _internalWorld(std::make_shared<InternalEngineWorld>()),
        _assetManager(std::make_shared<assets::AssetManager>()),
        _entityManager(std::make_shared<EntityManager>(_internalWorld->GetWorld())),
        _sceneManager(std::make_shared<scenes::SceneManager>(_assetManager, _entityManager)),
        _editorEventsRelay(std::make_shared<api::EditorEventsRelay>())
    {
        Services::GetInstance()->SetEngine(this);
        Services::GetInstance()->SetAssetManager(_assetManager);
        Services::GetInstance()->SetInternalWorld(_internalWorld->GetWorld());
        Services::GetInstance()->SetEntityManager(_entityManager);
        Services::GetInstance()->SetWindowManager(_windowManager);
        Services::GetInstance()->SetEditorEventsRelay(_editorEventsRelay);

        render::ShaderGenerator::GetInstance().Initialize();
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
                _mainThread->CompleteTasks();
            }

            _runEngine = true;
            _internalWorld->Configure(_app, _mainRenderer, _mainThread, _entityManager);
            _sceneManager->LoadScene(0);
            _app->OnStart();

            StartEvent();
        }
        catch (const std::exception& exc)
        {
            LOG_ERROR("Exception on engine start. {}", exc.what())
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
            LOG_ERROR("Exception in engine update loop, {}", exc.what())
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

    bool Engine::IsEditorMode() const
    {
        return _mode == EditorMode;
    }

    bool Engine::IsEditor() const
    {
        return _isEditor;
    }

    int Engine::GetExitCode() const
    {
        return _exitCode;
    }

    std::shared_ptr<Task> Engine::ExecuteOnMainThread(std::function<void()> fn) const
    {
        auto t = std::make_shared<Task>(fn);
        _mainThread->AddTask(t);
        return t;
    }
}



