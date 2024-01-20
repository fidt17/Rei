#pragma once
#include "App.h"
#include "Engine/Engine.h"
#include "Modules/EntityManagement/EntityManager.h"

#ifdef REI_APP

namespace rei
{
    class BehaviourComponentFactory;
}

extern std::shared_ptr<rei::App> CreateApp();
extern void ConfigureComponentsFactory(rei::BehaviourComponentFactory& factory);

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
        delete engine;
        return 0;
    }
}

int main()
{
    try
    {
        const auto engine = rei::external::CreateEngine();
        ConfigureComponentsFactory(rei::GetEntityManager()._componentFactory);
        rei::external::Start(engine);

        std::cin.get();

        rei::external::Shutdown(engine, 0);
    }
    catch (const std::exception& e)
    {
        LOG_ERROR("Exception", e.what())
        return -1;
    }

    return 0;
}

#endif
