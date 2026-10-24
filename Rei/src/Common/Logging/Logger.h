#pragma once
#include <string>
#include <vector>
#include <atomic>
#include <deque>
#include <mutex>

#include "LogLevelEnum.h"
#include "LogMessage.h"

namespace rei::common::logging
{
    REI_API std::vector<std::string> GetRecentLogEntriesSnapshot();

    class Logger
    {
        struct PendingMessage
        {
            LogLevelEnum Level;
            std::string Message;
            std::string Details;
        };

    public:
        using Callback = void (*)(const LogMessage&);
        eventpp::CallbackList<void(const LogMessage&)> NewLogEvent;
        
        explicit Logger(std::string loggerScope);

        REI_API void AddCallback(Callback callback);

        REI_API void Log(LogLevelEnum logLevel, const std::string& message) const;
        REI_API void Log(LogLevelEnum logLevel, const std::string& message, const std::string& details) const;

        void Enable();
        void Disable();
        void SetMinLogLevel(LogLevelEnum level);
        LogLevelEnum GetMinLogLevel() const;
        
    private:
        std::string _loggerScope;
        std::atomic<bool> _enabled = true;
        std::atomic<LogLevelEnum> _minLogLevel = Debug;
        mutable std::mutex _messageMutex;
        mutable std::deque<PendingMessage> _pendingMessages;
        mutable bool _publishing = false;
        std::vector<Callback> _callbacks;

        void PublishPendingMessages() const;
    };
}
