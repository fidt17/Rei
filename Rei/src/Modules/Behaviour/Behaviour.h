#pragma once

#define BEHAVIOUR_BODY(BEHAVIOUR_NAME)\
    public:\
    BEHAVIOUR_NAME() = default;\
    explicit BEHAVIOUR_NAME(const i32 id, const rei::ecs::Entity entity) : Behaviour(id, entity) {} ;\
    explicit BEHAVIOUR_NAME(const i32 id, const rei::ecs::Entity entity, const nlohmann::json& data);\
    BEHAVIOUR_NAME& operator=(const BEHAVIOUR_NAME& other) = default;\
    private:
#include "Ecs/RefComponent.h"
#include "Engine/Services.h"

namespace rei
{
    namespace transformation
    {
        class Transform;
    }

    class Behaviour
    {
    public:

        Behaviour() = default;
        REI_API explicit Behaviour(i32 id, ecs::Entity e);
        
        virtual ~Behaviour() = default;

        virtual void LoadAssets(assets::AssetManager& assetManager) { }
        virtual void Init() { }
        virtual void Start() { }
        virtual void Update() { }
        virtual void Dispose() { }

        i32 GetBehaviourId() const;
        
        REI_API ecs::Entity GetEntity() const;
        REI_API transformation::Transform& GetTransform() const;

        template <typename  T>
        REI_API ecs::RefComponent<T> GetComponent() const;

        Behaviour& operator=(const Behaviour& other) = default;

    private:
        i32 _id{};
        ecs::Entity _entity{-1, 0};
        ecs::RefComponent<transformation::Transform> _transform;
    };

    template <typename T>
    ecs::RefComponent<T> Behaviour::GetComponent() const
    {
        ECS_WORLD(GetInternalWorld());
        return GET_REF(_entity, T);
    }
}
