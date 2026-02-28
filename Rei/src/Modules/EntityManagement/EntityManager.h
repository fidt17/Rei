#pragma once
#include <typeindex>
#include <vector>

#include "Engine/Services.h"
#include "Modules/Assets/Core/AssetDependency.h"
#include "Modules/Behaviour/Components/BehaviourCollection.h"
#include "Modules/Scenes/SceneEntity.h"

namespace rei
{
    class BehaviourRegistry
    {
    public:
        using GetBehaviourDataMethod = std::function<nlohmann::json(ecs::Entity)>;
        using SetBehaviourDataMethod = std::function<void(ecs::Entity, const nlohmann::json&)>;
        using CollectAssetDependenciesMethod = std::function<void(const nlohmann::json&, std::vector<assets::AssetDependency>&)>;

        template <typename T>
        void RegisterComponent(i32 id, GetBehaviourDataMethod getJsonFunc,
                               SetBehaviourDataMethod setFromJsonFunc,
                               CollectAssetDependenciesMethod collectAssetDependenciesMethod = nullptr)
        {
            _addMethods.insert({
                id, [=](const ecs::Entity e, const nlohmann::json& data) -> T& {
                    ECS_WORLD(GetInternalWorld());

                    if (HAS(e, T))
                    {
                        REI_THROW("Entity " + std::string(e) + " already has a component " + STRING(id))
                    }

                    T& t = GET(e, T);

                    t = T(id, e);

                    if (!data.empty())
                    {
                        setFromJsonFunc(e, data);
                    }

                    GET(e, BehaviourCollection).Behaviours.push_back(id);

                    return t;
                }
            });

            _deleteMethods.insert({
                id, [=](const ecs::Entity e)
                {
                    ECS_WORLD(GetInternalWorld());
                    DEL(e, T);

                    auto& behavioursCollection = GET(e, BehaviourCollection);
                    behavioursCollection.Behaviours.erase(
                        std::remove(behavioursCollection.Behaviours.begin(), behavioursCollection.Behaviours.end(), id),
                        behavioursCollection.Behaviours.end());
                }
            });

            _getMethods.insert({
                id, [](const ecs::Entity e) -> T& {
                    ECS_WORLD(GetInternalWorld());
                    return GET(e, T);
                }
            });

            _getJsonMethods.insert({
                id, getJsonFunc
            });

            _setFromJsonMethods.insert({
                id, setFromJsonFunc
            });

            if (collectAssetDependenciesMethod != nullptr)
            {
                _collectAssetDependenciesMethods.insert({
                    id, collectAssetDependenciesMethod
                });
            }

            _behaviourIdMap[std::type_index(typeid(T))] = id;
        }

        Behaviour& AddBehaviour(const ecs::Entity e, const i32 id, const nlohmann::json& data) const
        {
            if (_addMethods.count(id) == 0)
                REI_THROW("Missing add behaviour method. Component ID: " + STRING(id))

            return _addMethods.at(id)(e, data);
        }

        Behaviour& GetBehaviour(const ecs::Entity e, const i32 id) const
        {
            if (_getMethods.count(id) == 0)
                REI_THROW("Missing get behaviour method. Component ID: " + STRING(id))

            return _getMethods.at(id)(e);
        }

        void DeleteBehaviour(const ecs::Entity e, const i32 id) const
        {
            if (_deleteMethods.count(id) == 0)
                REI_THROW("Missing delete behaviour method. Component ID: " + STRING(id))

            return _deleteMethods.at(id)(e);
        }

        nlohmann::json GetBehaviourData(const ecs::Entity e, const i32 id) const
        {
            if (_getJsonMethods.count(id) == 0)
                REI_THROW("Missing get behaviour data method. Component ID: " + STRING(id))

            return _getJsonMethods.at(id)(e);
        }

        void SetBehaviourData(const ecs::Entity e, const i32 id, const nlohmann::json& data) const
        {
            if (_setFromJsonMethods.count(id) == 0)
                REI_THROW("Missing set behaviour data method. Component ID: " + STRING(id))

            return _setFromJsonMethods.at(id)(e, data);
        }

        void CollectAssetDependencies(const i32 id, const nlohmann::json& data, std::vector<assets::AssetDependency>& outDependencies) const
        {
            if (!_collectAssetDependenciesMethods.contains(id)) return;

            _collectAssetDependenciesMethods.at(id)(data, outDependencies);
        }

        template <typename R>
        i32 GetId()
        {
            return _behaviourIdMap[std::type_index(typeid(R))];
        }

    private:
        std::unordered_map<i32, std::function<Behaviour&(ecs::Entity, const nlohmann::json&)>> _addMethods{};
        std::unordered_map<i32, std::function<void(ecs::Entity)>> _deleteMethods{};
        std::unordered_map<i32, std::function<Behaviour&(ecs::Entity)>> _getMethods{};
        std::unordered_map<i32, GetBehaviourDataMethod> _getJsonMethods{};
        std::unordered_map<i32, SetBehaviourDataMethod> _setFromJsonMethods{};
        std::unordered_map<i32, CollectAssetDependenciesMethod> _collectAssetDependenciesMethods{};
        std::map<std::type_index, i32> _behaviourIdMap;
    };

    class EntityManager
    {
    public:
        explicit EntityManager(const std::shared_ptr<ecs::World>& world);

        REI_API ecs::Entity GetBySceneId(i32 id) const;

        REI_API ecs::Entity CreateNewEntity(const std::string& name);
        REI_API void Create(const SceneEntity& sceneEntity) const;

        REI_API Behaviour& GetBehaviour(ecs::Entity e, i32 behaviourId) const;
        REI_API Behaviour& AddBehaviour(ecs::Entity e, i32 behaviourId, const nlohmann::json& data, bool init = true) const;

        template <typename T>
        REI_API T& AddBehaviour(const ecs::Entity e)
        {
            const i32 id = _behaviourRegistry.GetId<T>();
            return static_cast<T&>(AddBehaviour(e, id, nlohmann::json()));
        }

        REI_API void DeleteBehaviour(ecs::Entity e, i32 behaviourId);

        REI_API std::vector<ecs::Entity> GetRootEntities() const;

        REI_API ecs::Entity Instantiate(ecs::Entity source, const std::string& requestedName = "", bool includeChildren = true) const;

        REI_API void Destroy(ecs::Entity e) const;
        REI_API void ResolveTransformParents() const;

        void InitBehaviour(ecs::Entity e, Behaviour& b) const;

        BehaviourRegistry& GetBehaviourRegistry() { return _behaviourRegistry; }

    private:
        BehaviourRegistry _behaviourRegistry;

        std::shared_ptr<ecs::EcsRegistry> _ecs;
        std::shared_ptr<ecs::Filter> _entityInfoFilter;

        i32 GenerateNewSceneEntityId() const;
    };
}

#define ADD_BEHAVIOUR(e, T) rei::GetEntityManager().AddBehaviour<T>(e)
