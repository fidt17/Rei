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
        try
        {
            _action();
        }
        catch (...)
        {
            {
                std::scoped_lock lock(_completionMutex);
                _isComplete = true;
            }
            _completionCondition.notify_all();
            throw;
        }

        {
            std::scoped_lock lock(_completionMutex);
            _isComplete = true;
        }
        _completionCondition.notify_all();
    }

    void Task::WaitForCompletion() const
    {
        std::unique_lock lock(_completionMutex);
        _completionCondition.wait(lock, [&] { return _isComplete; });
    }
}
