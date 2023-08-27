#include "Logger.h"

namespace rei::logging
{
    void Logger::AddLogCallback(REI_EVENT_ACTION(const LogMessage&) callback)
    {
        _newLogEvent += callback;
    }

    void Logger::RemoveLogCallback(REI_EVENT_ACTION(const LogMessage&) callback)
    {
        _newLogEvent -= callback;
    }

    void Logger::Log(const LogLevelEnum logLevel, const std::string& message) const
    {
        Log("", logLevel, message, "");
    }

    void Logger::Log(const std::string& scope, const LogLevelEnum logLevel, const std::string& message) const
    {
        Log(scope, logLevel, message, "");
    }

    void Logger::Log(const std::string& scope, const LogLevelEnum logLevel, const std::string& message, const std::string& details) const
    {
        const auto logMessage = LogMessage(scope.c_str(), logLevel, message.c_str(), details.c_str());
        std::cout << logMessage << std::endl;
        _newLogEvent.Invoke(logMessage);
    }
}
