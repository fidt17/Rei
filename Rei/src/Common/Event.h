#pragma once

#include <memory>
#include <vector>

namespace rei
{
    template <typename T>
    class Event
    {
    public:
        Event& operator +=(std::shared_ptr<T> mFunc)
        {
            _subscribers.push_back(mFunc);
            return *this;
        }

        Event& operator -=(std::shared_ptr<T> mFunc)
        {
            _subscribers.erase(remove(_subscribers.begin(), _subscribers.end(), mFunc));
            return *this;
        }

        template <typename... Ts>
        void Invoke(Ts ... args) const
        {
            for (auto f : _subscribers)
            {
                try
                {
                    (*f)(args...);
                }
                catch (...)
                {
                }
            }
        }

    private:
        std::vector<std::shared_ptr<T>> _subscribers;
    };
}

#define REI_EVENT(...) Event<std::function<void(__VA_ARGS__)>>
#define REI_EVENT_ACTION(...) const std::shared_ptr<std::function<void(__VA_ARGS__)>>&
