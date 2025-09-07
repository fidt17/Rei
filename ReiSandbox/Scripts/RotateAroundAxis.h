#pragma once
#include <rei_behaviours/transformation/Transform.h>

class RotateAroundAxis : public rei::Behaviour
{
private:
    BEHAVIOUR_BODY(RotateAroundAxis)
    SERIALIZE rei::math::Vector3 _rotationSpeed;

public:
    void Update() override
    {
        return;
        auto& rotation = GetTransform().GetRotation();
        rotation.x += _rotationSpeed.x;
        rotation.y += _rotationSpeed.y;
        rotation.z += _rotationSpeed.z;
    }
};
