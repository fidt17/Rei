#pragma once

namespace rei::engine
{
    // TODO: move to separate file
    // TODO: support return values
    class Task
    {
    public:
        Task(const std::function<void()>& action) :
            _isComplete(false),
            _action(action)
        {
        }

        Task(const Task& other) = delete;

        void Invoke()
        {
            _action();
            _isComplete = true;
        }

        void WaitForCompletion() const
        {
            while (!_isComplete) { }
        }

    private:
        bool _isComplete;
        std::function<void()> _action;
    };

    class MainThread
    {
    public:
        MainThread() { }
        MainThread(const MainThread& other) = delete;

        void Run();
        REI_API void AddTask(std::shared_ptr<Task>& t);

    private:
        std::queue<std::shared_ptr<Task>> _tasksQueue;
    };
}
