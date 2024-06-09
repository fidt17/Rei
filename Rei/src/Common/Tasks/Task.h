#pragma once

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
        bool _isComplete;
        std::function<void()> _action;
    };
}
