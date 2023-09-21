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
        World()
            : _ecsRegistry(std::make_shared<EcsRegistry>()),
              _filterRegistry(std::make_shared<FiltersRegistry>())
        {
            _ecsRegistry->MaxComponentIdChangedEvent += std::make_shared<std::function<void(u32)>>([this](const u32 s){UpdateBitMasks(s);});
        }

        void Refresh();

        std::shared_ptr<EcsRegistry> GetRegistry();
        std::shared_ptr<FiltersRegistry> GetFiltersRegistry();

    private:
        std::shared_ptr<EcsRegistry> _ecsRegistry;
        std::shared_ptr<FiltersRegistry> _filterRegistry;

        void UpdateBitMasks(u32 size) const;
    };
}
