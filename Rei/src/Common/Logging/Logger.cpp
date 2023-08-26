#include "Logger.h"

namespace rei
{
    void Logger::AddLogCallback(const std::shared_ptr<std::function<void(const std::string&)>>& callback)
    {
        _newLogEvent += callback;
    }

    void Logger::RemoveLogCallback(const std::shared_ptr<std::function<void(const std::string&)>>& callback)
    {
        _newLogEvent -= callback;
    }

    void Logger::Log(const std::string& message) const
    {
        std::cout << message << std::endl;
        _newLogEvent.Invoke(message);
    }
}
