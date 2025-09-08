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
        auto& rotation = GetTransform().GetRotation();
        rotation.x += _rotationSpeed.x;
        rotation.y += _rotationSpeed.y;
        rotation.z += _rotationSpeed.z;
    }

    void DrawGizmos(const rei::render::Gizmos& gizmos) override
    {
        auto& transform = GetTransform();
        const auto& position = transform.GetPosition();

        const f32 size = 3;

        gizmos.DrawWireframeBox(position, {size, size, size}, GetTransform().GetRotation(), {1,0,0,1});

        gizmos.DrawLine(position, position + transform.GetRight() * 3, rei::render::Color::Red(), false);
        gizmos.DrawLine(position, position + transform.GetUp() * 3, rei::render::Color::Green(), false);
        gizmos.DrawLine(position, position + transform.GetForward() * 3, rei::render::Color::Blue(), false);
    }
};
