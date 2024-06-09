#pragma once
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
        std::queue<std::shared_ptr<Task>> _tasksQueue;
    };
}
