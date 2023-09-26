#include "pch.h"
#include "ReiMainThread.h"

namespace rei::internal::main_thread
{
    void ReiMainThread::Run()
    {
        REI_ASSERT(_thread == nullptr, "Main thread is already running");

        _runFlag = true;
        _thread = new std::thread([&]()
        {
            while (_runFlag)
            {
                try
                {
                    _onUpdateEvent.Invoke();
                }
                catch (const std::exception& e)
                {
                    LOG_ERROR("Exception in main thread", e.what())
                }

                std::this_thread::sleep_for(std::chrono::seconds(1));
            }
        });
    }

    void ReiMainThread::Stop()
    {
        _runFlag = false;
        _thread->join();
        delete _thread;
    }

    void ReiMainThread::AddOnUpdateCallback(const std::shared_ptr<std::function<void()>>& callback)
    {
        _onUpdateEvent += callback;
    }

    void ReiMainThread::RemoveOnUpdateCallback(const std::shared_ptr<std::function<void()>>& callback)
    {
        _onUpdateEvent -= callback;
    }
}
