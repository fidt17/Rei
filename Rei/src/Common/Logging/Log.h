#pragma once
#include <memory>

#include "Logger.h"

namespace rei::logging
{
    class REI_API Log
    {
    public:
        inline static void Initialize();
        inline static std::shared_ptr<Logger> GetLogger();
        inline static void AddLogCallback(REI_EVENT_ACTION(const LogMessage&) logCallback);

    private:
        inline static std::shared_ptr<Logger> _logger;
    };
}

const std::string LOG_SCOPE;

#define SET_LOG_SCOPE(x) const std::string LOG_SCOPE = (x);\

#ifdef _DEBUG
    #define LOG(...) rei::logging::Log::GetLogger()->Log(LOG_SCOPE, rei::logging::LogLevelEnum::Info, __VA_ARGS__);
    #define LOG_WARNING(...) rei::logging::Log::GetLogger()->Log(LOG_SCOPE, rei::logging::LogLevelEnum::Warning, __VA_ARGS__);
    #define LOG_ERROR(...) rei::logging::Log::GetLogger()->Log(LOG_SCOPE, rei::logging::LogLevelEnum::Error, __VA_ARGS__);
#else
    #define LOG(...) 
    #define LOG_WARNING(...) 
    #define LOG_ERROR(...) 
#endif