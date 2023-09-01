#pragma once

#include "Engine.h"
#include "Common/IFactory.h"

namespace rei
{
    class EngineFactory : public IFactory<Engine>
    {
    public:
        Engine CreateInstance() const override;
    };
}
