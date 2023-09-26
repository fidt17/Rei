#pragma once
#include "App.h"
#include "Engine/Engine.h"

#ifdef REI_APP

extern std::shared_ptr<rei::App> CreateApp();

namespace rei::external
{
    REI_EXTERN_API inline internal::engine::Engine* CreateEngine()
    {
        return new internal::engine::Engine(CreateApp());
    }

    REI_EXTERN_API inline void Start(internal::engine::Engine* engine)
    {
        engine->Start();
    }

    REI_EXTERN_API inline int Shutdown(internal::engine::Engine* engine, const int exitCode)
    {
        engine->Shutdown(exitCode);
        return 0;
    }
}

int main()
{
    auto engine = rei::external::CreateEngine();
    rei::external::Start(engine);

    std::cin.get();

    rei::external::Shutdown(engine, 0);

    delete engine;

    return 0;
}

#endif
