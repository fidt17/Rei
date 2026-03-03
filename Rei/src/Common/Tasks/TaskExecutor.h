#pragma once
#include <mutex>
#include <queue>

#include "Task.h"

namespace rei
{
    class TaskExecutor
    {
    public:
        TaskExecutor() = default;
        TaskExecutor(const TaskExecutor& other) = delete;

        void CompleteTasks();
        REI_API void AddTask(std::shared_ptr<Task>& t);

    private:
        std::mutex _tasksQueueMutex;
        std::queue<std::shared_ptr<Task>> _tasksQueue;
    };
}
