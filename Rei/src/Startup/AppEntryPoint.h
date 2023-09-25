#pragma once
#include "EngineEntryPoint.h"

#ifdef REI_APP

    std::shared_ptr<rei::internal::engine::Engine> g_Engine;

    extern std::shared_ptr<rei::App> CreateApp();

    REI_EXTERN_API void Initialize()
    {
        auto entryPoint = rei::internal::entry_point::EngineEntryPoint();
        entryPoint.Initialize();
        g_Engine = entryPoint.CreateEngine(CreateApp());
    }

    REI_EXTERN_API inline void Start()
    {
        g_Engine->Start();
    }

    REI_EXTERN_API inline int Shutdown(const int exitCode)
    {
        g_Engine->Shutdown(exitCode);
        return 0;
    }

    int main()
    {
        Initialize();
        Start();

        std::cin.get();

        Shutdown(0);

        return 0;
    }

#endif
