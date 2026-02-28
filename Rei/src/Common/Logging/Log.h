#pragma once
#include <format>
#include <memory>
#include <source_location>
#include <string_view>

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

    namespace internal
    {
        REI_API std::string BuildFunctionName(std::string_view signature);
        REI_API std::string BuildSourceDetails(std::source_location location = std::source_location::current());
        REI_API std::string BuildDetails(std::string_view extraDetails, std::source_location location = std::source_location::current());
    }

    namespace utility
    {
        REI_API std::string FormatSize(i64 bytes);
        REI_API std::string FormatDurationMs(i64 durationMs);
        REI_API std::string SimplifyTypeName(std::string_view rawTypeName);
    }
}

#ifdef DEBUG
    #define LOG_DEBUG(...) rei::common::logging::Log::GetLogger()->Log(rei::common::logging::LogLevelEnum::Debug, std::format(__VA_ARGS__), rei::common::logging::internal::BuildSourceDetails());
    #define LOG(...) rei::common::logging::Log::GetLogger()->Log(rei::common::logging::LogLevelEnum::Info, std::format(__VA_ARGS__), rei::common::logging::internal::BuildSourceDetails());
    #define LOG_WARNING(...) rei::common::logging::Log::GetLogger()->Log(rei::common::logging::LogLevelEnum::Warning, std::format(__VA_ARGS__), rei::common::logging::internal::BuildSourceDetails());
    #define LOG_ERROR(...) rei::common::logging::Log::GetLogger()->Log(rei::common::logging::LogLevelEnum::Error, std::format(__VA_ARGS__), rei::common::logging::internal::BuildSourceDetails());
    #define LOG_DEBUG_D(extra_details, ...) rei::common::logging::Log::GetLogger()->Log(rei::common::logging::LogLevelEnum::Debug, std::format(__VA_ARGS__), rei::common::logging::internal::BuildDetails((extra_details)));
    #define LOG_D(extra_details, ...) rei::common::logging::Log::GetLogger()->Log(rei::common::logging::LogLevelEnum::Info, std::format(__VA_ARGS__), rei::common::logging::internal::BuildDetails((extra_details)));
    #define LOG_WARNING_D(extra_details, ...) rei::common::logging::Log::GetLogger()->Log(rei::common::logging::LogLevelEnum::Warning, std::format(__VA_ARGS__), rei::common::logging::internal::BuildDetails((extra_details)));
    #define LOG_ERROR_D(extra_details, ...) rei::common::logging::Log::GetLogger()->Log(rei::common::logging::LogLevelEnum::Error, std::format(__VA_ARGS__), rei::common::logging::internal::BuildDetails((extra_details)));

    #define LOGGER_ENABLE() rei::common::logging::Log::GetLogger()->Enable();
    #define LOGGER_DISABLE() rei::common::logging::Log::GetLogger()->Disable();
    #define LOG_USE_COUNT(x) LOG("Use count of " + std::string(#x) + " = " + STRING(x.use_count()))
#else
    #define LOG_DEBUG(...)
    #define LOG(...)
    #define LOG_WARNING(...)
    #define LOG_ERROR(...)
    #define LOG_DEBUG_D(extra_details, ...)
    #define LOG_D(extra_details, ...)
    #define LOG_WARNING_D(extra_details, ...)
    #define LOG_ERROR_D(extra_details, ...)
    #define LOGGER_ENABLE() rei::common::logging::Log::GetLogger()->Enable();
    #define LOGGER_DISABLE() rei::common::logging::Log::GetLogger()->Disable();
    #define LOG_USE_COUNT(x)
#endif
