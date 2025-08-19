#pragma once
#include <rei_behaviours/transformation/Transform.h>

class TestMovement : public rei::Behaviour
{
private:
    BEHAVIOUR_BODY(TestMovement)
    SERIALIZE f32 _radius = 10;
    SERIALIZE f32 _speed = 0.01f;
    SERIALIZE bool _actionFlag;

    f32 time = 0;

public:
    void Update() override
    {
        time += _speed * 1e-02;

        auto& position = GetTransform().GetPosition();
        position.x = cos(time) * _radius;
        position.z = sin(time) * _radius;

        GetTransform().GetRotation().z = time * 10;

        if (_actionFlag)
        {
            _actionFlag = false;
            TriggerAction();
        }
    }

private:
    void TriggerAction()
    {
        _speed *= 2;
    }
};
