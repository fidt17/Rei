#pragma once
#include "Modules/Render/Renderer.h"
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
        REI_API void Start();
        REI_API void Shutdown(int exitCode);

        REI_API int GetExitCode() const;

    private:
        bool _runEngine = false;
        window::WindowManager _windowManager;
        MainWindowHandler _mainWindowHandler;
        render::Renderer _renderer;

        int _exitCode;
        
        std::shared_ptr<App> _app;
        std::shared_ptr<ecs::World> _internalWorld;

        std::shared_ptr<assets::AssetManager> _assetManager;
        std::shared_ptr<EntityManager> _entityManager;
        std::shared_ptr<scenes::SceneManager> _sceneManager;

        void RunUpdateLoop();
        void ConfigureMainWindow();
    };
}
