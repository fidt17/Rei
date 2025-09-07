#pragma once

namespace rei::ecs
{
    template <typename T>
    class RefComponent
    {
    public:
        RefComponent() :
            _ecs(nullptr),
            _entity(NULL_ENTITY)
        {
        }

        RefComponent(const std::shared_ptr<EcsRegistry>& w, const Entity e) : _ecs(w), _entity(e)
        {
            w->Get<T>(e);
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

    private:
        std::shared_ptr<EcsRegistry> _ecs;
        Entity _entity;
    };
}
