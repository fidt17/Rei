#include "pch.h"
#include "TaskExecutor.h"

namespace rei
{
    void TaskExecutor::CompleteTasks()
    {
        while (true)
        {
            std::shared_ptr<Task> task;
            {
                std::scoped_lock lock(_tasksQueueMutex);
                if (_tasksQueue.empty()) break;

                task = _tasksQueue.front();
                _tasksQueue.pop();
            }
            
            const auto& t = task;
            t->Invoke();
        }
    }

    void TaskExecutor::AddTask(std::shared_ptr<Task>& t)
    {
        std::scoped_lock lock(_tasksQueueMutex);
        _tasksQueue.push(t);
    }
}
