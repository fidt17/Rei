#pragma once
#include "Common/Tasks/TaskExecutor.h"
#include "Modules/Render/Renderer.h"
#include "Startup/App.h"

namespace rei::internal::engine
{
    class InternalEngineWorld
    {
    public:
        void Configure(const std::shared_ptr<App>& app, const std::shared_ptr<render::Renderer>& renderer, const std::shared_ptr<TaskExecutor>& mainThread, const std::shared_ptr<EntityManager>& entityManager) const;
        void Run() const;

        std::shared_ptr<ecs::World> GetWorld();

    private:
        std::shared_ptr<ecs::World> _world = std::make_shared<ecs::World>();
    };
}
