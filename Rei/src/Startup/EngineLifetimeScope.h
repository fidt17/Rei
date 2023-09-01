#pragma once
#include "Engine/Engine.h"

namespace rei
{
    class EngineLifetimeScope
    {
    public:
        void Configure();

        std::shared_ptr<IFactory<Engine>> GetEngineFactory() const { return _appFactory; }
    private:
        std::shared_ptr<IFactory<Engine>> _appFactory = nullptr;
    };
}
