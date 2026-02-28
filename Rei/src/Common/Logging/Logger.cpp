#include "Logger.h"
#include "windows.h"
#include <mutex>

namespace rei::common::logging
{
    namespace
    {
        std::mutex g_consoleWriteMutex;
    }

    Logger::Logger(std::string loggerScope): _loggerScope(std::move(loggerScope))
    {
    }

    void Logger::Log(const LogLevelEnum logLevel, const std::string& message) const
    {
        Log(logLevel, message, "");
    }

    void UpdateConsoleColor(const LogLevelEnum logLevel)
    {
        switch (logLevel)
        {
        case Debug:
            SetConsoleTextAttribute(GetStdHandle(STD_OUTPUT_HANDLE), 8);
            break;
        case Info:
            SetConsoleTextAttribute(GetStdHandle(STD_OUTPUT_HANDLE), 15);
            break;
        case Warning:
            SetConsoleTextAttribute(GetStdHandle(STD_OUTPUT_HANDLE), 14);
            break;
        case Error:
            SetConsoleTextAttribute(GetStdHandle(STD_OUTPUT_HANDLE), 12);
            break;
        }
    }

    void Logger::Log(const LogLevelEnum logLevel, const std::string& message, const std::string& details) const
    {
        if (!_enabled) return;
        if (logLevel < _minLogLevel) return;

        const auto logMessage = LogMessage("Engine", logLevel, message.c_str(), details.c_str());
        {
            const std::lock_guard lock(g_consoleWriteMutex);
            UpdateConsoleColor(logLevel);

            if (logLevel == Error)
            {
                std::cout << "\n";
            }

            std::cout << logMessage << "\n";
        }

        NewLogEvent(logMessage);
    }

    void Logger::Enable()
    {
        _enabled = true;
    }

    void Logger::Disable()
    {
        _enabled = false;
    }

    void Logger::SetMinLogLevel(const LogLevelEnum level)
    {
        _minLogLevel = level;
    }

    LogLevelEnum Logger::GetMinLogLevel() const
    {
        return _minLogLevel;
    }
}
