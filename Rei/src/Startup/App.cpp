#include "App.h"
#include <thread>
#include <chrono>

namespace rei
{
#define LOG(x) MessageEvent.Invoke((x))

    void App::Configure()
    {
        LOG("Configure App");
    }

    void App::Run()
    {
        _runApp = true;
        _appRunning = true;

        LOG("Run App");
        int counter = 0;

        while (_runApp)
        {
            LOG("Counter: " + std::to_string(counter));

            counter += 1;
            std::this_thread::sleep_for(std::chrono::seconds(1));
        }

        _appRunning = false;
    }

    int App::Shutdown(int exitCode)
    {
        _runApp = false;
        LOG("Shutdown App. Exit code: " + std::to_string(exitCode));

        while (_appRunning) { }

        return exitCode;
    }

    void App::AddLogCallback(const std::shared_ptr<std::function<void(const std::string&)>>& logCallback)
    {
        if (!logCallback) return;
        MessageEvent += logCallback;
    }
}
