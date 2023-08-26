#pragma once
#include "Application/App.h"
#include "EngineLifetimeScope.h"

namespace rei
{
    class REI_API EngineEntryPoint
    {
    public:

        void ConfigureEngine();
        std::shared_ptr<App> CreateApplication() const;

    private:
        EngineLifetimeScope _scope;
    };
}

REI_API int main();
