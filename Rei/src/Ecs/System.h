#pragma once
#include "EcsRegistry.h"
#include "FiltersRegistry.h"

namespace rei::ecs
{
    class System
    {
    public:
        explicit System(std::shared_ptr<EcsRegistry> ecs, std::shared_ptr<FilterProvider> filters)
            : _ecs(std::move(ecs)), _filters(std::move(filters))
        {
        }
        
        virtual ~System() = default;

        virtual void OnUpdate() = 0;

    protected:
    
        #define FOR(e, f) REI_ASSERT_NOT_NULL(f);\
            for (const auto (e) : *(f))

        const std::shared_ptr<EcsRegistry> _ecs;
        const std::shared_ptr<FilterProvider> _filters;
    };
}
