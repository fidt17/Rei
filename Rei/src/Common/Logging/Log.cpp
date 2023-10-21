#include "Log.h"

namespace rei::common::logging
{
    void Log::Initialize()
    {
        _logger = std::make_shared<Logger>("Core");
    }

    std::shared_ptr<Logger> Log::GetLogger()
    {
        if (_logger == nullptr)
        {
            Initialize();
        }
        
        return _logger;
    }

    void Log::AddLogCallback(REI_EVENT_DELEGATE(const LogMessage&) logCallback)
    {
        if (_logger == nullptr)
        {
            Initialize();
        }
        
        _logger->AddLogCallback(logCallback);
    }
}
