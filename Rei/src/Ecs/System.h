#pragma once
#include "EcsRegistry.h"
#include "FiltersRegistry.h"

namespace rei::ecs
{
    class System
    {
    public:
        explicit System(std::shared_ptr<EcsRegistry> ecs, std::shared_ptr<FiltersRegistry> filtersRegistry)
            : _ecs(std::move(ecs)), _filtersRegistry(std::move(filtersRegistry))
        {
        }

        virtual void OnUpdate() = 0;

    protected:
#define RUN(f, FOREACH_BLOCK) { for (const auto e : (f)->Entities()) { FOREACH_BLOCK } }
    
        const std::shared_ptr<EcsRegistry> _ecs;
        const std::shared_ptr<FiltersRegistry> _filtersRegistry;
    };
}
