#include "Logger.h"
#include "windows.h"
#include <deque>
#include <mutex>

namespace rei::common::logging
{
    namespace
    {
        std::mutex _consoleWriteMutex;
        std::mutex _recentLogsMutex;
        std::deque<std::string> _recentLogs;
        constexpr size_t _recentLogsCapacity = 256;
    }

    std::vector<std::string> GetRecentLogEntriesSnapshot()
    {
        const std::lock_guard lock(_recentLogsMutex);
        return {_recentLogs.begin(), _recentLogs.end()};
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

        std::string snapshotLine;
        {
            const std::lock_guard lock(_consoleWriteMutex);
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

            snapshotLine = std::string(level) + " | " + message;
            if (!details.empty())
            {
                snapshotLine += " | " + details;
            }
        }

        {
            const std::lock_guard snapshotLock(_recentLogsMutex);
            if (_recentLogs.size() >= _recentLogsCapacity)
            {
                _recentLogs.pop_front();
            }

            _recentLogs.push_back(std::move(snapshotLine));
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
