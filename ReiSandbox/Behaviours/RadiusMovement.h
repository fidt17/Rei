#pragma once
#include <rei_behaviours/transformation/Transform.h>

class RadiusMovement : public rei::Behaviour
{
private:
    BEHAVIOUR_BODY(RadiusMovement)

    SERIALIZE f32 _offset;

    f32 _counter = 0;
    
public:
    void Update() override;
};
