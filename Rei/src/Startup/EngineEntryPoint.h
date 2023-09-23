#pragma once
#include "EngineLifetimeScope.h"
#include "Engine/Engine.h"

namespace rei
{
    class EngineEntryPoint
    {
    public:
        REI_API void ConfigureFramework();
        REI_API std::shared_ptr<Engine> CreateEngine() const;

    private:
        EngineLifetimeScope _scope;
    };
}

#ifdef REI_APP

    extern void OnProjectStart();
    extern void OnProjectShutdown();

    int main()
    {
        auto entryPoint = rei::EngineEntryPoint();
        
        entryPoint.ConfigureFramework();
        const auto engine = entryPoint.CreateEngine();
        engine->Start();
        OnProjectStart();

        std::cin.get();
        
        engine->Shutdown(1);
        OnProjectShutdown();

        return 0;
    }

#endif
