#include <typeindex>

#include "Modules/Components/EntityInfo.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "Modules/UpdateLoop/Components/UpdateCallback.h"

class MyComponent
{
};

namespace rei::internal
{
    void AddBehaviourComponent(const ecs::Entity e, const i32 id)
    {
        ECS_WORLD(rei::GetInternalWorld());
        if (id == 0)
        {
            GET(e, MyComponent);
            GET(e, update_loop::UpdateCallback).Callback = [=]
            {
                LOG("Hello there! My name is " + GET(e, EntityInfo).Name)
            };
        }
        else
        {
            REI_THROW("Missing component generation definition. Component ID: " + STRING(id))
        }
    }
}

class ProjectApplication final : public rei::App
{
public:
    void OnStart() override
    {
        LOG("APP START")
    }

    int i = 1;

    void OnUpdate() override
    {
        LOG("APP UPDATE")

        ECS_WORLD(rei::GetInternalWorld());
        auto e = rei::GetEntityManager().GetBySceneId(1);

        LOG("Found scene entity with id: " + STRING(i++) + ". Name: " + GET(e, EntityInfo).Name + ". Has my component: " + STRING(HAS(e, MyComponent)))

        rei::GetEntityManager().AddBehaviour(e, 0);
    }

    void OnShutdown() override
    {
        LOG("APP SHUTDOWN")
    }
};

std::shared_ptr<rei::App> CreateApp() { return std::make_shared<ProjectApplication>(); }
