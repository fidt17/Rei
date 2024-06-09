#pragma once
#include "Common/Tasks/TaskExecutor.h"
#include "Modules/RenderingModule/Renderer.h"
#include "Modules/Window/MainWindowHandler.h"
#include "Modules/Window/WindowManager.h"

namespace rei
{
    class EntityManager;

    namespace scenes
    {
        class SceneManager;
    }

    namespace assets
    {
        class AssetManager;
    }

    class App;
}

namespace rei::internal::engine
{
    class Engine
    {
    public:
        REI_API explicit Engine(std::shared_ptr<App> app);
        Engine(const Engine& e) = delete;
        
        REI_API void Start();
        REI_API void Shutdown(int exitCode);

        REI_API int GetExitCode() const;

        REI_API std::shared_ptr<window::Window> CreateMainWindow();
        REI_API TaskExecutor& GetMainThread() const;

    private:
        bool _runEngine = false;
        int _exitCode;

        window::WindowManager _windowManager;
        MainWindowHandler _mainWindowHandler;

        std::shared_ptr<TaskExecutor> _reiMainThread;
        std::shared_ptr<render::Renderer> _renderer;
        
        std::shared_ptr<App> _app;
        std::shared_ptr<ecs::World> _internalWorld;

        std::shared_ptr<assets::AssetManager> _assetManager;
        std::shared_ptr<EntityManager> _entityManager;
        std::shared_ptr<scenes::SceneManager> _sceneManager;

        void ConfigureInternalWorld();
        void RunUpdateLoop();
    };
}
