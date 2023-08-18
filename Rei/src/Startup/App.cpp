#include "App.h"
#include <thread>
#include <chrono>

namespace rei
{
    void App::Start()
    {
        while (true)
        {
            std::this_thread::sleep_for(std::chrono::seconds(1));
            this->_appNumber += 1;
        }
    }

    int App::GetAppNumber() const
    {
        return this->_appNumber;
    }
}
