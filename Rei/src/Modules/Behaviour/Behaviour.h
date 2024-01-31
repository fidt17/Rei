#pragma once

#define BEHAVIOUR_BODY(BEHAVIOUR_NAME)\
    public:\
    BEHAVIOUR_NAME() = default;\
    explicit BEHAVIOUR_NAME(const rei::ecs::Entity entity, const nlohmann::json& data);\
    BEHAVIOUR_NAME& operator=(const BEHAVIOUR_NAME& other) = default;\
    private:

#define SERIALIZED

namespace rei
{
    class Behaviour
    {
    public:
        virtual ~Behaviour() = default;

        Behaviour() = default;

        explicit Behaviour(const ecs::Entity e) : _entity(e)
        {
        }

        virtual void Init() = 0;

        REI_API ecs::Entity GetEntity() const;

        Behaviour& operator=(const Behaviour& other) = default;

    private:
        ecs::Entity _entity{-1, 0};
    };
}
