#include "EngineLifetimeScope.h"

#include "Application/AppFactory.h"

namespace rei
{
    void EngineLifetimeScope::Configure()
    {
        _appFactory = std::make_unique<AppFactory>();
    }
}
