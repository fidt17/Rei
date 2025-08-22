#pragma once
#include "Common/Tasks/TaskExecutor.h"
#include "Modules/Render/Renderer.h"
#include "Modules/Scenes/SceneManager.h"
#include "Modules/Window/MainWindowHandler.h"
#include "Modules/Window/WindowManager.h"
#include "Startup/App.h"

namespace rei::internal::engine
{
    class Engine
    {
    public:
        eventpp::CallbackList<void(int)> ShutdownEvent;
        
        REI_API explicit Engine(std::shared_ptr<App> app);
        Engine(const Engine& e) = delete;
        
        REI_API void Start();
        REI_API void Shutdown(int exitCode);

        REI_API int GetExitCode() const;

        REI_API std::shared_ptr<window::Window> CreateMainWindow(i32 width, i32 height, bool hideByDefault);
        REI_API std::shared_ptr<Task> ExecuteOnMainThread(std::function<void()>) const;

    private:
        bool _runEngine = false;
        int _exitCode;

        std::shared_ptr<window::WindowManager> _windowManager;
        std::shared_ptr<window::MainWindowHandler> _mainWindowHandler;

        std::shared_ptr<TaskExecutor> _reiMainThread;
        std::shared_ptr<render::Renderer> _mainRenderer;
        
        std::shared_ptr<App> _app;
        std::shared_ptr<ecs::World> _internalWorld;

        std::shared_ptr<assets::AssetManager> _assetManager;
        std::shared_ptr<EntityManager> _entityManager;
        std::shared_ptr<scenes::SceneManager> _sceneManager;
        std::shared_ptr<input::Input> _input;

        void SetupGLFW() const;
        void ConfigureInternalWorld() const;
        void RunUpdateLoop();
    };
}
