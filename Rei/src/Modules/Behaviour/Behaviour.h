#pragma once

#include "Ecs/RefComponent.h"
#include "Engine/Services.h"

namespace rei
{
    namespace render
    {
        class Gizmos;
    }

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
        virtual void DrawGizmos(const render::Gizmos&) { }

        REI_API i32 GetBehaviourId() const;
        
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
