#pragma once

#include <thread>

namespace rei::internal::engine
{
    class Engine
    {
    public:
        REI_API explicit Engine(std::shared_ptr<App> app);
        REI_API void Start();
        REI_API void Shutdown(int exitCode);

    private:
        std::thread _mainThread;
        bool _mainThreadRunFlag = false;

        std::shared_ptr<App> _app;
        ecs::World _ecsWorld;

        void OnUpdate();
    };
}
