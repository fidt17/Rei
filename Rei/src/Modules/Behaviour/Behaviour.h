#pragma once

#include "Ecs/ComponentRef.h"
#include "Engine/Services.h"

namespace rei
{
    namespace render
    {
        class Gizmos;
    }

    class Transform;

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
        virtual void BeforeREI_GET() { }
        virtual void AfterREI_SET() { }

        REI_API i32 GetBehaviourId() const;
        
        REI_API ecs::Entity GetEntity() const;
        REI_API Transform& GetTransform() const;

        template <typename  T>
        REI_API ecs::ComponentRef<T> GetComponent() const;

        Behaviour& operator=(const Behaviour& other) = default;

        REI_API bool IsEnabled() const;
        virtual REI_API void Enable();
        virtual REI_API void Disable();

    private:
        i32 _id{};
        ecs::Entity _entity{-1, 0};
        ecs::ComponentRef<Transform> _transform;

        bool _enabled = true;
    };

    template <typename T>
    ecs::ComponentRef<T> Behaviour::GetComponent() const
    {
        ECS_WORLD(GetInternalWorld());
        return GET_REF(_entity, T);
    }
}
