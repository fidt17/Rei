#include "pch.h"
#include "MainThread.h"

namespace rei::engine
{
    void MainThread::Run()
    {
        while (!_tasksQueue.empty())
        {
            const auto t = _tasksQueue.front();
            _tasksQueue.pop();
            t->Invoke();
        }
    }

    void MainThread::AddTask(std::shared_ptr<Task>& t)
    {
        _tasksQueue.push(t);
    }
}
