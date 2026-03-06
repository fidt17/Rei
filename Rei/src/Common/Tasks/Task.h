#pragma once
#include <condition_variable>
#include <functional>
#include <mutex>

namespace rei
{
    // TODO: support return values
    class Task
    {
    public:
        REI_API Task(const std::function<void()>& action);
        Task(const Task& other) = delete;

        void Invoke();

        REI_API void WaitForCompletion() const;

    private:
        mutable std::condition_variable _completionCondition;
        mutable std::mutex _completionMutex;
        bool _isComplete;
        std::function<void()> _action;
    };
}
