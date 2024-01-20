#include "Startup/AppEntryPoint.h"
#include "Modules/EntityManagement/EntityManager.h"

class MyBehaviour : public rei::Behaviour
{
public:
    void Init() override
    {
        LOG("------------------------")
    }
};

void ConfigureComponentsFactory(rei::BehaviourComponentFactory& factory)
{
    factory.RegisterComponent<MyBehaviour>(0);
}

class ProjectApplication final : public rei::App
{
public:
    void OnStart() override
    {
        LOG("APP START")
    }

    void OnUpdate() override
    {
    }

    void OnShutdown() override
    {
        LOG("APP SHUTDOWN")
    }
};

std::shared_ptr<rei::App> CreateApp() { return std::make_shared<ProjectApplication>(); }
