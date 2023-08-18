
#include <iostream>

#include "App.h"
#include <thread>

int main()
{
    auto app = sandbox::App();
    std::thread appThread([&]() { app.Start(); });

    while (true)
    {
        std::this_thread::sleep_for(std::chrono::seconds(1));
        std::cout << "N: " << app.GetAppNumber() << std::endl;
    }
    
    return 2;
}
