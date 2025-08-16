#pragma once
#include <rei_behaviours/transformation/Transform.h>

class TestMovement : public rei::Behaviour
{
private:
    BEHAVIOUR_BODY(TestMovement)
    SERIALIZE f32 _radius = 10;
    SERIALIZE f32 _speed = 0.01f;

    f32 time = 0;

public:
    void Update() override
    {
        time += _speed * 1e-02;

        auto& position = GetTransform().GetPosition();
        position.x = cos(time) * _radius;
        position.z = sin(time) * _radius;

        GetTransform().GetRotation().z = time * 10;

        Test();
    }

    void Test()
    {
        const auto& e = GetEntity();
        if (e == rei::ecs::NULL_ENTITY) return;

        nlohmann::json data;
        data["EntityId"] = e.Id;
        data["EntityGeneration"] = e.Generation;

        ECS_WORLD(rei::GetInternalWorld());
        const auto& entityInfo = GET(e, EntityInfo);
        data["SceneId"] = entityInfo.Id;
        data["Name"] = entityInfo.Name;
        data["Behaviours"] = nlohmann::json::array();

        for (const auto behaviour : entityInfo.Behaviours)
        {
            data["Behaviours"].push_back(rei::GetEntityManager().GetBehaviourRegistry().GetBehaviourData(e, behaviour));
        }

        std::cout << data.dump(4) << std::endl;
    }
};
