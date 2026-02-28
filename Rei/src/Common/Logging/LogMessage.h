#pragma once
#include <chrono>
#include <ctime>
#include <iomanip>
#include <iostream>

#include "LogLevelEnum.h"

namespace rei::common::logging
{
    struct LogMessage
    {
        const char* Scope;
        LogLevelEnum Level;
        const char* Message;
        const char* Details;

        LogMessage(const char* scope, const LogLevelEnum level, const char* message, const char* details)
            : Scope(scope),
              Level(level),
              Message(message),
              Details(details)
        {
        }
    };

    inline std::ostream& operator <<(std::ostream& stream, LogMessage const& message)
    {
        const auto now = std::chrono::system_clock::now();
        const auto nowTime = std::chrono::system_clock::to_time_t(now);
        std::tm localTime{};
        localtime_s(&localTime, &nowTime);

        const char* level = "INFO";
        switch (message.Level)
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

        stream << "[" << std::put_time(&localTime, "%H:%M:%S") << "]"
               << "[" << level << "] "
               << message.Message;

        if (message.Details[0])
        {
            stream << "\nDetails: " << message.Details << "\n";
        }

        return stream;
    }
}
