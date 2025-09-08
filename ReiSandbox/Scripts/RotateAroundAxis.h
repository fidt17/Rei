#pragma once
#include <rei_behaviours/transformation/Transform.h>

#include "Modules/Render/Modules/Gizmos.h"

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

    void DrawGizmos(const rei::render::Gizmos& gizmos) override
    {
        auto& transform = GetTransform();
        gizmos.RenderWireframeBox(transform.GetPosition(), rei::math::Vector3::One() * 3, {}, rei::render::Color::Red(), false);
    }
};
