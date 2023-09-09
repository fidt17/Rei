#include "Core.h"
#include "EngineFactory.h"

namespace rei
{
    Engine EngineFactory::CreateInstance() const
    {
        return Engine();
    }
}
