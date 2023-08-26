#pragma once
#include <memory>

#include "Logger.h"

namespace rei
{
    class REI_API Log
    {
    public:
        inline static void Initialize();
        inline static std::shared_ptr<Logger> GetLogger();
        inline static void AddLogCallback(const std::shared_ptr<std::function<void(const std::string&)>>& logCallback);

    private:
        inline static std::shared_ptr<Logger> _logger;
    };
}


#define LOG(...) rei::Log::GetLogger()->Log((__VA_ARGS__));
