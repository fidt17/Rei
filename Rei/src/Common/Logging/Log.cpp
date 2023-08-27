#include "Log.h"

namespace rei::logging
{
    void Log::Initialize()
    {
        _logger = std::make_shared<Logger>("Core");
    }

    std::shared_ptr<Logger> Log::GetLogger()
    {
        return _logger;
    }

    void Log::AddLogCallback(REI_EVENT_ACTION(const LogMessage&) logCallback)
    {
        _logger->AddLogCallback(logCallback);
    }
}
