#pragma once
#include "Application/App.h"

namespace rei
{
    class EngineLifetimeScope
    {
    public:
        void Configure();

        std::shared_ptr<IFactory<App>> GetAppFactory() const { return _appFactory; }
    private:
        std::shared_ptr<IFactory<App>> _appFactory = nullptr;
    };
}
