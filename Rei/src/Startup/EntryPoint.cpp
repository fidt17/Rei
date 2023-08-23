#include "EntryPoint.h"

#include <iostream>
#include <ostream>
#include <thread>

namespace rei
{
    App* Application;
    std::thread AppThread;

    void StartEngine()
    {
        if (Application) return;

        std::cout << "Create App" << std::endl;
        Application = new App();
        Application->Configure();

        AppThread = std::thread([]()
        {
            std::cout << "Run App thread" << std::endl;
            Application->Run();
            std::cout << "Finish App thread" << std::endl;
        });
        AppThread.detach();
    }

    int StopEngine(int exitCode)
    {
        if (!Application) return -1;
        std::cout << "Stop ENGINE" << std::endl;

        Application->Shutdown(exitCode);
        delete Application;

        return exitCode;
    }

    App* GetApp() { return Application; }
}
