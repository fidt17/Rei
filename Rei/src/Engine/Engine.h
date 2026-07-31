#pragma once
#include <atomic>

#include "InternalEngineWorld.h"
#include "Api/EditorEventsRelay.h"
#include "Common/Diagnostics/DiagnosticsService.h"
#include "Common/Tasks/TaskExecutor.h"
#include "Modules/Render/Renderer.h"
#include "Modules/Scenes/SceneManager.h"
#include "Modules/Window/MainWindowHandler.h"
#include "Modules/Window/WindowManager.h"
#include "Startup/App.h"

namespace rei::internal::engine
{
    enum EngineMode
    {
        EditorMode = 0,
        PlayMode = 1
    };

    class Engine
    {
    public:
        REI_EVENT() StartEvent;
        REI_EVENT(i32) ShutdownEvent;

        REI_API explicit Engine(std::shared_ptr<App> app, EngineMode mode, bool isEditor);
        Engine(const Engine& e) = delete;

        REI_API void Start();
        REI_API void Shutdown(i32 exitCode);

        REI_API bool IsPlaymode() const;
        REI_API bool IsEditorMode() const;
        REI_API bool IsEditor() const;
        REI_API bool IsRunning() const;

        REI_API i32 GetExitCode() const;

        REI_API std::shared_ptr<window::Window> CreateMainWindow(const WindowCreationSettings& settings);
        REI_API std::shared_ptr<Task> ExecuteOnMainThread(std::function<void()>) const;
        REI_API bool RequestFrameCapture(const render::FrameCaptureCallback& callback) const;

    private:
        EngineMode _mode;
        bool _isEditor;

        std::atomic<bool> _runEngine = false;
        i32 _exitCode;

        std::shared_ptr<window::WindowManager> _windowManager;
        std::shared_ptr<window::MainWindowHandler> _mainWindowHandler;

        std::shared_ptr<TaskExecutor> _mainThread;
        std::shared_ptr<render::Renderer> _mainRenderer;

        std::shared_ptr<App> _app;
        std::shared_ptr<InternalEngineWorld> _internalWorld;

        std::shared_ptr<assets::AssetManager> _assetManager;
        std::shared_ptr<EntityManager> _entityManager;
        std::shared_ptr<scenes::SceneManager> _sceneManager;

        std::shared_ptr<api::EditorEventsRelay> _editorEventsRelay;
        std::shared_ptr<common::diagnostics::DiagnosticsService> _diagnostics;

        void RunUpdateLoop();
    };
}
