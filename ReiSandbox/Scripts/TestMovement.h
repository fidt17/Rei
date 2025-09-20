#pragma once
#include <rei_behaviours/transformation/Transform.h>

class TestMovement : public rei::Behaviour
{
private:
    BEHAVIOUR_BODY(TestMovement)
    SERIALIZE f32 _radius = 10;
    SERIALIZE f32 _speed = 0.01f;
    SERIALIZE bool _horizontalMovement;

    f32 time = 0;

public:
    void Update() override
    {
        time -= _speed * 1e-02;

        auto& position = GetTransform().GetPosition();

        if (_horizontalMovement)
        {
            position.x = cos(time) * _radius;
            position.z = sin(time) * _radius;
        }
        else
        {
            position.x = cos(time) * _radius;
            position.y = sin(time) * _radius;
        }
    }

private:
};
