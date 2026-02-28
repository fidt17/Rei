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

        {
            const std::lock_guard lock(g_consoleWriteMutex);
            UpdateConsoleColor(logLevel);

            if (logLevel == Error)
            {
                std::cout << "\n";
            }

            const auto now = std::chrono::system_clock::now();
            const auto nowTime = std::chrono::system_clock::to_time_t(now);
            std::tm localTime{};
            localtime_s(&localTime, &nowTime);

            const char* level = "INFO";
            switch (logLevel)
            {
            case Debug:
                level = "DEBUG";
                break;
            case Info:
                level = "INFO";
                break;
            case Warning:
                level = "WARN";
                break;
            case Error:
                level = "ERROR";
                break;
            }

            std::cout << "[" << std::put_time(&localTime, "%H:%M:%S") << "]"
                      << "[" << level << "] "
                      << message;

            if (!details.empty())
            {
                std::cout << "\nDetails: " << details << "\n";
            }

            std::cout << "\n";
        }

        const auto logMessage = LogMessage("Engine", logLevel, message.c_str(), details.c_str());
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
