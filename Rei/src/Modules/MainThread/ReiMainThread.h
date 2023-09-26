#pragma once
#include <thread>

namespace rei::internal::main_thread
{
    class ReiMainThread
    {
    public:
    
        void Run();
        void Stop();

        void AddOnUpdateCallback(REI_EVENT_DELEGATE());
        void RemoveOnUpdateCallback(REI_EVENT_DELEGATE());
        
    private:
        REI_ACTION _onUpdateEvent;
        
        std::thread* _thread = nullptr;
        bool _runFlag = false;
    };
}
