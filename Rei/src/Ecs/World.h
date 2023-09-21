#pragma once
#include "EcsRegistry.h"
#include "FiltersRegistry.h"

namespace rei::ecs
{
    class EcsRegistry;
    class FiltersRegistry;

    class World : public std::enable_shared_from_this<World>
    {
    public:
        World();

        void Refresh();

        std::shared_ptr<EcsRegistry> GetRegistry();
        std::shared_ptr<FiltersRegistry> GetFiltersRegistry();

    private:
        std::shared_ptr<EcsRegistry> _ecsRegistry;
        std::shared_ptr<FiltersRegistry> _filterRegistry;

        void UpdateBitMasks(u32 size) const;
    };
}
