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
        time += _speed;
        
        auto& position = GetTransform().GetPosition();
        position.x = cos(time);
        position.y = sin(time);
        position.z = position.x + position.y;
    }
};
