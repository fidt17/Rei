#pragma once
#include <string>
#include <vector>

#include "LogLevelEnum.h"
#include "LogMessage.h"

namespace rei::common::logging
{
    REI_API std::vector<std::string> GetRecentLogEntriesSnapshot();

    class Logger
    {
    public:
        eventpp::CallbackList<void(const LogMessage&)> NewLogEvent;
        
        explicit Logger(std::string loggerScope);

        REI_API void Log(LogLevelEnum logLevel, const std::string& message) const;
        REI_API void Log(LogLevelEnum logLevel, const std::string& message, const std::string& details) const;

        void Enable();
        void Disable();
        void SetMinLogLevel(LogLevelEnum level);
        LogLevelEnum GetMinLogLevel() const;
        
    private:
        std::string _loggerScope;
        bool _enabled = true;
        LogLevelEnum _minLogLevel = Debug;
    };
}
