#pragma once

#include "Engine/Services.h"
#include "Modules/Components/EntityInfo.h"

namespace rei::ecs
{
#ifdef SERIALIZABLE_BODY
#pragma push_macro("SERIALIZABLE_BODY")
#undef SERIALIZABLE_BODY
#define SERIALIZABLE_BODY(CLASS_NAME)\
        public:\
        CLASS_NAME() = default;\
        nlohmann::json REI_GET() const;\
        void REI_SET(const nlohmann::json& data);\
        void ResolveDependencies();
#endif

    template <typename T>
    class ComponentRef
    {
    public:
        SERIALIZABLE_BODY(ComponentRef)

        SERIALIZE i32 SceneEntityId = 0;

        ComponentRef(const std::shared_ptr<EcsRegistry>& w, const Entity e) :
            _ecs(w),
            _entity(e)
        {
            w->Get<T>(e);

            if (!w->IsDead(e) && w->Has<EntityInfo>(e))
            {
                SceneEntityId = w->Get<EntityInfo>(e).Id;
            }
        }

        T& Get() const
        {
            REI_ASSERT(!IsNull(), std::format("Null component reference {} on {}", typeid(T).name(), std::string(_entity)))

            return _ecs->Get<T>(_entity);
        }

        constexpr operator T&() const noexcept
        {
            REI_ASSERT(!IsNull(), std::format("Null component reference {} on {}", typeid(T).name(), std::string(_entity)))

            return _ecs->Get<T>(_entity);
        }

        bool IsNull() const
        {
            if (_ecs == nullptr) return true;
            if (IS_DEAD(_entity)) return true;
            if (!HAS(_entity, T)) return true;

            return false;
        }

        void Resolve()
        {
            _ecs = GetInternalWorld()->GetRegistry();
            _entity = NULL_ENTITY;

            if (SceneEntityId == 0) return;

            const auto entity = GetEntityManager().GetBySceneId(SceneEntityId);
            if (IS_DEAD(entity) || !HAS(entity, T)) return;

            _entity = entity;
        }

    private:
        std::shared_ptr<EcsRegistry> _ecs = nullptr;
        Entity _entity = NULL_ENTITY;
    };

#ifdef SERIALIZABLE_BODY
#pragma pop_macro("SERIALIZABLE_BODY")
#endif
}
