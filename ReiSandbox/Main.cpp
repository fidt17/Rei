#include "Startup/AppEntryPoint.h"
#include "Modules/EntityManagement/EntityManager.h"

class MyBehaviour : public rei::Behaviour
{
    BEHAVIOUR_BODY(MyBehaviour)
    
    SERIALIZED std::string _property;

public:
    void Init() override
    {
        LOG("------------------------")
    }
};

MyBehaviour::MyBehaviour(const rei::ecs::Entity entity, const nlohmann::json& data)
    : Behaviour(entity),
      _property(data.at("_property"))
{
}

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
