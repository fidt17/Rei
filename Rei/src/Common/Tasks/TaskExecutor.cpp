#include "pch.h"
#include "Common/Profiling/ProfileMarkers.h"
#include "TaskExecutor.h"

namespace rei
{
    void TaskExecutor::CompleteTasks()
    {
        REI_PROFILE_SCOPE(profiling::markers::TASKS.Id);
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
            profiling::Count(profiling::markers::TASK_COUNT.Id);
            t->Invoke();
        }
    }

    void TaskExecutor::AddTask(std::shared_ptr<Task>& t)
    {
        std::scoped_lock lock(_tasksQueueMutex);
        _tasksQueue.push(t);
    }
}
