#include "Log.h"

namespace rei
{
    void Log::Initialize()
    {
        _logger = std::make_shared<Logger>();
    }

    std::shared_ptr<Logger> Log::GetLogger()
    {
        return _logger;
    }

    void Log::AddLogCallback(const std::shared_ptr<std::function<void(const std::string&)>>& logCallback)
    {
        _logger->AddLogCallback(logCallback);
    }
}
