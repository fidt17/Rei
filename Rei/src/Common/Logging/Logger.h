#pragma once
#include <string>

#include "LogLevelEnum.h"
#include "LogMessage.h"

namespace rei::common::logging
{
    class Logger
    {
    public:
        eventpp::CallbackList<void(const LogMessage&)> NewLogEvent;
        
        explicit Logger(std::string loggerScope);

        REI_API void Log(LogLevelEnum logLevel, const std::string& message) const;
        REI_API void Log(const std::string& scope, LogLevelEnum logLevel, const std::string& message) const;
        REI_API void Log(const std::string& scope, LogLevelEnum logLevel, const std::string& message, const std::string& details) const;

        void Enable();
        void Disable();
        
    private:
        std::string _loggerScope;
        bool _enabled = true;
    };
}
