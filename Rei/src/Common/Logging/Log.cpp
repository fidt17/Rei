#include "Log.h"
#include <cstdlib>

namespace rei::common::logging
{
    static LogLevelEnum ParseLogLevel(const std::string& value)
    {
        if (value == "debug" || value == "DEBUG")
        {
            return Debug;
        }
        if (value == "warning" || value == "WARNING" || value == "warn" || value == "WARN")
        {
            return Warning;
        }
        if (value == "error" || value == "ERROR")
        {
            return Error;
        }

        return Debug;
    }

    void Log::Initialize()
    {
        _logger = std::make_shared<Logger>("Core");

        const char* logLevelEnv = std::getenv("REI_LOG_LEVEL");
        if (logLevelEnv != nullptr && logLevelEnv[0] != '\0')
        {
            _logger->SetMinLogLevel(ParseLogLevel(logLevelEnv));
        }
    }

    std::shared_ptr<Logger> Log::GetLogger()
    {
        if (_logger == nullptr)
        {
            Initialize();
        }

        return _logger;
    }
}
