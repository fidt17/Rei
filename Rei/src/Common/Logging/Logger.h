#pragma once
#include <functional>
#include <string>

#include "Common/Event.h"

namespace rei
{
    class REI_API Logger
    {
    public:
        void AddLogCallback(const std::shared_ptr<std::function<void(const std::string&)>>&);
        void RemoveLogCallback(const std::shared_ptr<std::function<void(const std::string&)>>&);
        
        void Log(const std::string& message) const;

    private:
        Event<std::function<void(const std::string&)>> _newLogEvent;
    };
}
