#pragma once
#include <rei_behaviours/transformation/Transform.h>

class RadiusMovement : public rei::Behaviour
{
private:
    BEHAVIOUR_BODY(RadiusMovement)

    SERIALIZE f32 _offset;

    f32 _counter = 0;
    
public:
    void Update() override
    {
        auto& position = GetTransform().GetPosition();

        position.x = sin(_counter + _offset);
        position.z = cos(_counter + _offset);

        _counter += 0.01f;

        LOG(STRING(GetEntity().Id) + " " + STRING(_counter + _offset) + " " + STRING(sin(_counter + _offset)))
    }
};
