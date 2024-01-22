#pragma once

#define BEHAVIOUR(NAME)\
    class NAME : public rei::Behaviour  // NOLINT(bugprone-macro-parentheses)

struct EntityInfo;

namespace rei
{
    class Behaviour
    {
    public:
        virtual ~Behaviour() = default;

        void Construct(ecs::Entity e, const EntityInfo& entityInfo);
        virtual void Init() = 0;

        REI_API ecs::Entity GetEntity() const;
        REI_API const std::string& GetName() const;
        REI_API i32 GetSceneId() const;

    private:
        ecs::Entity _entity {-1, 0};
        i32 _sceneId = -1;
    };
}
