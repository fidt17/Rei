#include "Modules/Components/EntityInfo.h"

class EntityManager
{
public:
    rei::ecs::Entity GetBySceneId(const i32 id)
    {
        auto w = rei::GetInternalWorld();
        ECS_WORLD(w);
        
        const auto f = w.GetFiltersRegistry()->Get<EntityInfo>();
        
        FOR(e, f)
        {
            if (GET(e, EntityInfo).Id == id)
            {
                return e;
            }
        }

        return rei::ecs::NULL_ENTITY;
    }
};

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
        auto e = EntityManager().GetBySceneId(i);
        if (e == rei::ecs::NULL_ENTITY)
        {
            LOG("Could not find entity with id: " + STRING(i))
            i = 1;
            return;
        }

        LOG("Found scene entity with id: " + STRING(i++) + ". Name: " + GET(e, EntityInfo).Name)
    }

    void OnShutdown() override
    {
        LOG("APP SHUTDOWN")
    }
};

std::shared_ptr<rei::App> CreateApp() { return std::make_shared<ProjectApplication>(); }
