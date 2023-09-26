#pragma once
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
        if (message.Scope[0])
        {
            stream << "[" << message.Scope << "] ";
        }

        stream << message.Message;

        if (message.Details[0])
        {
            stream << "\nDetails: " << message.Details << "\n";
        }

        return stream;
    }
}
