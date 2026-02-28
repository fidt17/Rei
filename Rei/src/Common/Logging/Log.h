#pragma once
#include <memory>

#include "Logger.h"

namespace rei::common::logging
{
    class Log
    {
    public:
        REI_API static void Initialize();
        REI_API static std::shared_ptr<Logger> GetLogger();

    private:
        inline static std::shared_ptr<Logger> _logger;
    };
}

const std::string LOG_SCOPE;

#define SET_LOG_SCOPE(x) const std::string LOG_SCOPE = (x);

#ifdef DEBUG
    #define LOG_DEBUG(...) rei::common::logging::Log::GetLogger()->Log(LOG_SCOPE, rei::common::logging::LogLevelEnum::Debug, std::format(__VA_ARGS__));
    #define LOG(...) rei::common::logging::Log::GetLogger()->Log(LOG_SCOPE, rei::common::logging::LogLevelEnum::Info, std::format(__VA_ARGS__));
    #define LOG_WARNING(...) rei::common::logging::Log::GetLogger()->Log(LOG_SCOPE, rei::common::logging::LogLevelEnum::Warning, std::format(__VA_ARGS__));
    #define LOG_ERROR(...) rei::common::logging::Log::GetLogger()->Log(LOG_SCOPE, rei::common::logging::LogLevelEnum::Error, std::format(__VA_ARGS__));
    #define LOGGER_ENABLE() rei::common::logging::Log::GetLogger()->Enable();
    #define LOGGER_DISABLE() rei::common::logging::Log::GetLogger()->Disable();
    #define LOG_USE_COUNT(x) LOG("Use count of " + std::string(#x) + " = " + STRING(x.use_count()))
#else
    #define LOG_DEBUG(...)
    #define LOG(...) 
    #define LOG_WARNING(...) 
    #define LOG_ERROR(...) 
    #define LOGGER_ENABLE() rei::common::logging::Log::GetLogger()->Enable();
    #define LOGGER_DISABLE() rei::common::logging::Log::GetLogger()->Disable();
#define LOG_USE_COUNT(x) 
#endif

