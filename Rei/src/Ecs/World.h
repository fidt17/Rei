#pragma once
#include "EcsRegistry.h"
#include "FiltersRegistry.h"
#include "IEcsModule.h"

namespace rei::ecs
{
    class System;
    class IEcsModule;

    class World : public std::enable_shared_from_this<World>
    {
    public:
        REI_API World();
        
        template<typename T>
        REI_API void AddSystem(){
            _systems.emplace_back(std::make_shared<T>(shared_from_this()));
        }

        template<typename T, typename... Args>
        REI_API void AddSystem(Args... args){
            _systems.emplace_back(std::make_shared<T>(shared_from_this(), args...));
        }

        REI_API void AddSystem(const std::function<void()>& fn);

        template<typename T>
        REI_API void AddModule()
        {
            T module = T();
            module.AddSystems(shared_from_this());
        }

        REI_API void Run() const;
        REI_API void Refresh() const;
        REI_API void RefreshAll() const;

        REI_API std::shared_ptr<EcsRegistry> GetRegistry();
        REI_API std::shared_ptr<FiltersRegistry> GetFiltersRegistry();

    private:
        std::shared_ptr<EcsRegistry> _ecsRegistry;
        std::shared_ptr<FiltersRegistry> _filterRegistry;
        std::vector<std::shared_ptr<System>> _systems;

        void UpdateBitMasks(size_t size) const;
    };
}
