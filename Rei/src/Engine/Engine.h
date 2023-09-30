#pragma once

#include <thread>

#include "Modules/Assets/AssetManager.h"
#include "Modules/MainThread/ReiMainThread.h"

namespace rei
{
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

    private:
        main_thread::ReiMainThread _mainThread;
        std::shared_ptr<App> _app;
        std::shared_ptr<ecs::World> _ecsWorld;
        std::shared_ptr<assets::AssetManager> _assetManager;

        void OnUpdate() const;
    };
}
