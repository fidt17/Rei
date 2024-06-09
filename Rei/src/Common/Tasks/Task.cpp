#include "pch.h"
#include "Task.h"

namespace rei
{
    Task::Task(const std::function<void()>& action):
        _isComplete(false),
        _action(action)
    {
    }

    void Task::Invoke()
    {
        _action();
        _isComplete = true;
    }

    void Task::WaitForCompletion() const
    {
        while (!_isComplete)
        {
        }
    }
}
