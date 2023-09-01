#include "Engine.h"
#include <thread>
#include <chrono>

namespace rei
{
    SET_LOG_SCOPE("ENGINE")
    
    void Engine::Configure()
    {
        LOG("Configure")
    }

    void Engine::Start()
    {
        LOG("Run")

        _mainThread = std::thread([&]()
        {
            _mainThreadRunFlag = true;
            while (_mainThreadRunFlag)
            {
                try
                {
                    OnUpdate();
                }
                catch (const std::exception& e)
                {
                    LOG_ERROR("Exception in main thread", e.what())
                }

                std::this_thread::sleep_for(std::chrono::seconds(1));
            }
        });
    }

    void Engine::Shutdown(const int exitCode)
    {
        LOG("Shutdown. Exit code: " + std::to_string(exitCode))

        _mainThreadRunFlag = false;
        _mainThread.join();
    }

    int Counter = 0;
    
    void Engine::OnUpdate()
    {
        LOG("On Update", "Counter = " + std::to_string(Counter++))
    }
}
