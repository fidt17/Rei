#include "pch.h"
#include "Common/Profiling/ProfileMarkers.h"
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
        _app(std::move(app)),
        _time(std::make_shared<time::TimeService>()),
        _internalWorld(std::make_shared<InternalEngineWorld>()),
        _assetManager(std::make_shared<assets::AssetManager>()),
        _entityManager(std::make_shared<EntityManager>(_internalWorld->GetWorld())),
        _sceneManager(std::make_shared<scenes::SceneManager>(_assetManager, _entityManager)),
        _editorEventsRelay(std::make_shared<api::EditorEventsRelay>()),
        _diagnostics(std::make_shared<common::diagnostics::DiagnosticsService>()),
        _profiler(std::make_shared<profiling::ProfilingService>(mode))
    {
        Services::GetInstance()->SetEngine(this);
        Services::GetInstance()->SetTime(_time);
        Services::GetInstance()->SetAssetManager(_assetManager);
        Services::GetInstance()->SetInternalWorld(_internalWorld->GetWorld());
        Services::GetInstance()->SetEntityManager(_entityManager);
        Services::GetInstance()->SetWindowManager(_windowManager);
        Services::GetInstance()->SetEditorEventsRelay(_editorEventsRelay);
        Services::GetInstance()->SetDiagnostics(_diagnostics);
        Services::GetInstance()->SetProfiler(_profiler);
        _profiler->Register(profiling::markers::ALL);

        render::ShaderGenerator::GetInstance().Initialize();
        _mainRenderer = std::make_shared<render::Renderer>(_app->CreateCustomRenderModules());
    }

    Engine::~Engine()
    {
        // ComponentRef fields can keep the registry alive after World expires.
        // Finalize this engine's entities without touching a replacement World.
        {
            const auto world = _internalWorld->GetWorld();
            const auto registry = world->GetRegistry();
            for (const auto entity : registry->GetAllEntities())
            {
                if (registry->IsAlive(entity)) registry->DestroyEntity(entity);
            }
            world->Refresh();
        }
        _mainRenderer.reset();
        _sceneManager.reset();
        _entityManager.reset();
        _internalWorld.reset();
        Services::GetInstance()->ReleaseEngineServices(this);
    }

    std::shared_ptr<window::Window> Engine::CreateMainWindow(const WindowCreationSettings& settings)
    {
        auto mainWindow = _mainWindowHandler->CreateMainWindow(*_windowManager, settings);
        mainWindow->WindowClosingEvent.append([this](const window::Window&)
        {
            _mainRenderer->Dispose();
        });

        _mainWindowHandler->MainWindowClosedEvent.append([&]
        {
            LOG("Main window was closed")
            ExecuteOnMainThread([&]
            {
                Shutdown(MAIN_WINDOW_CLOSED_EXIT_CODE);
            });
        });

        mainWindow->SizeChangedEvent.append([&](const i32 width, const i32 height)
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

            _time->Reset();
            _runEngine.store(true);
            _internalWorld->Configure(_app, _mainRenderer, _mainThread, _entityManager);
            _sceneManager->LoadScene(0);
            _app->OnStart();

            LOG_DEBUG("Invoking start event")
            StartEvent();
        }
        catch (const std::exception& exc)
        {
            LOG_ERROR("Exception on engine start. {}", exc.what())
            Shutdown(ENGINE_INITIALIZATION_ERROR_EXIT_CODE);
            return;
        }

        RunUpdateLoop();
    }

    void Engine::RunUpdateLoop()
    {
        LOG_DEBUG("Engine update loop started")
        
        try
        {
            while (_runEngine.load())
            {
                _profiler->BeginFrame();
                _time->BeginFrame();
                _internalWorld->Run();
                _profiler->EndFrame();
                _profiler->FlushLogDump();
            }
        }
        catch (const std::exception& exc)
        {
            LOG_ERROR("Exception in engine update loop, {}", exc.what())
            Shutdown(ENGINE_UPDATE_ERROR_EXIT_CODE);
        }
    }

    void Engine::Shutdown(const i32 exitCode)
    {
        if (!_runEngine.exchange(false)) return;

        LOG("Engine shutdown")

        _exitCode = exitCode;

        _profiler->Shutdown();
        _app->OnShutdown();
        _sceneManager->Shutdown();
        _mainRenderer->Dispose();
        _assetManager->UnloadAllAssets();
        _assetManager->DeleteTmpFiles();
        glfwTerminate();

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

    bool Engine::IsRunning() const
    {
        return _runEngine.load();
    }

    i32 Engine::GetExitCode() const
    {
        return _exitCode;
    }

    bool Engine::RequestFrameCapture(const render::FrameCaptureCallback& callback) const
    {
        return _mainRenderer->RequestFrameCapture(callback);
    }

    std::shared_ptr<Task> Engine::ExecuteOnMainThread(std::function<void()> fn) const
    {
        auto t = std::make_shared<Task>(fn);
        _mainThread->AddTask(t);
        return t;
    }
}



