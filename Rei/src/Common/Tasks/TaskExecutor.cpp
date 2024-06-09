#include "pch.h"
#include "TaskExecutor.h"

namespace rei
{
    void TaskExecutor::CompleteTasks()
    {
        while (!_tasksQueue.empty())
        {
            const auto t = _tasksQueue.front();
            _tasksQueue.pop();
            t->Invoke();
        }
    }

    void TaskExecutor::AddTask(std::shared_ptr<Task>& t)
    {
        _tasksQueue.push(t);
    }
}
