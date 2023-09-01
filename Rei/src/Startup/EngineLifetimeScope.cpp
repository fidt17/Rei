#include "EngineLifetimeScope.h"

#include "Engine/EngineFactory.h"

namespace rei
{
    void EngineLifetimeScope::Configure()
    {
        _appFactory = std::make_unique<EngineFactory>();
    }
}
