#pragma once

#define BEHAVIOUR_BODY(BEHAVIOUR_NAME)\
    public:\
    BEHAVIOUR_NAME() = default;\
    explicit BEHAVIOUR_NAME(const i32 id, const rei::ecs::Entity entity, const nlohmann::json& data);\
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

        explicit Behaviour(const i32 id, const ecs::Entity e) : _id(id), _entity(e)
        {
        }

        virtual void Init() {}
        virtual void Start() {}
        virtual void Update() {}
        virtual void Dispose() {}

        i32 GetId() const;
        REI_API ecs::Entity GetEntity() const;

        Behaviour& operator=(const Behaviour& other) = default;

    private:
        i32 _id{};
        ecs::Entity _entity{-1, 0};
    };
}
