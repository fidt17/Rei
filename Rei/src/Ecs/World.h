#pragma once
#include "EcsRegistry.h"
#include "FiltersRegistry.h"
#include "System.h"

namespace rei::ecs
{
    class EcsRegistry;
    class FiltersRegistry;

    class World : public std::enable_shared_from_this<World>
    {
    public:
        World();
        
        template<typename T>
        void AddSystem(){
            _systems.emplace_back(std::make_shared<T>(GetRegistry(), GetFiltersRegistry()));
        }

        template<typename T, typename... Args>
        void AddSystem(Args... args){
            _systems.emplace_back(std::make_shared<T>(GetRegistry(), GetFiltersRegistry(), args...));
        }

        void Run();
        void Refresh();

        std::shared_ptr<EcsRegistry> GetRegistry();
        std::shared_ptr<FiltersRegistry> GetFiltersRegistry();

    private:
        std::shared_ptr<EcsRegistry> _ecsRegistry;
        std::shared_ptr<FiltersRegistry> _filterRegistry;
        std::vector<std::shared_ptr<System>> _systems;

        void UpdateBitMasks(u32 size) const;
    };
}
