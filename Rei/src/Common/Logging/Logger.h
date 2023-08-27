#pragma once
#include <functional>
#include <string>

#include "LogLevelEnum.h"
#include "LogMessage.h"
#include "Common/Event.h"

namespace rei::logging
{
    class REI_API Logger
    {
    public:
        explicit Logger(std::string loggerScope)
            : _loggerScope(std::move(loggerScope))
        {
        }

        void AddLogCallback(REI_EVENT_ACTION(const LogMessage&));
        void RemoveLogCallback(REI_EVENT_ACTION(const LogMessage&));

        void Log(LogLevelEnum logLevel, const std::string& message) const;
        void Log(const std::string& scope, LogLevelEnum logLevel, const std::string& message) const;
        void Log(const std::string& scope, LogLevelEnum logLevel, const std::string& message, const std::string& details) const;

    private:
        std::string _loggerScope;
        REI_EVENT(const LogMessage&) _newLogEvent;
    };
}
