#pragma once
#include <functional>
#include <string>

#include "LogLevelEnum.h"
#include "LogMessage.h"
#include "Common/Event.h"

namespace rei::common::logging
{
    class Logger
    {
    public:
        explicit Logger(std::string loggerScope)
            : _loggerScope(std::move(loggerScope))
        {
        }

        void AddLogCallback(REI_EVENT_DELEGATE(const LogMessage&));
        void RemoveLogCallback(REI_EVENT_DELEGATE(const LogMessage&));

        REI_API void Log(LogLevelEnum logLevel, const std::string& message) const;
        REI_API void Log(const std::string& scope, LogLevelEnum logLevel, const std::string& message) const;
        REI_API void Log(const std::string& scope, LogLevelEnum logLevel, const std::string& message, const std::string& details) const;

        void Enable();
        void Disable();
        
    private:
        REI_EVENT(const LogMessage&) _newLogEvent;
        
        std::string _loggerScope;
        bool _enabled = true;
    };
}
