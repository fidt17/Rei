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
        REI_API World();
        
        template<typename T>
        REI_API void AddSystem(){
            _systems.emplace_back(std::make_shared<T>(GetRegistry(), GetFiltersRegistry()));
        }

        template<typename T, typename... Args>
        REI_API void AddSystem(Args... args){
            _systems.emplace_back(std::make_shared<T>(GetRegistry(), GetFiltersRegistry(), args...));
        }

        REI_API void Run();
        REI_API void Refresh();

        REI_API std::shared_ptr<EcsRegistry> GetRegistry();
        REI_API std::shared_ptr<FiltersRegistry> GetFiltersRegistry();

    private:
        std::shared_ptr<EcsRegistry> _ecsRegistry;
        std::shared_ptr<FiltersRegistry> _filterRegistry;
        std::vector<std::shared_ptr<System>> _systems;

        void UpdateBitMasks(size_t size) const;
    };
}
