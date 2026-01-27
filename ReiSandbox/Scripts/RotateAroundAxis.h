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
        GetTransform().RotateWorld(_rotationSpeed.x, {1, 0, 0});
        GetTransform().RotateWorld(_rotationSpeed.y, {0, 1, 0});
        GetTransform().RotateWorld(_rotationSpeed.z, {0, 0, 1});

        rei::GetGizmos().EnqueueDrawCommand([&](rei::render::Gizmos& g)
        {
            auto& transform = GetTransform();
            const auto& position = transform.GetPosition();

            const f32 size = 3;

            //g.DrawWireframeBox(position, {size, size, size}, GetTransform().GetRotation(), {1, 0, 0, 1});

            g.DrawLine(position, position + transform.GetRight() * 1, rei::render::Color::Red(), false);
            g.DrawLine(position, position + transform.GetUp() * 1, rei::render::Color::Green(), false);
            g.DrawLine(position, position + transform.GetForward() * 1, rei::render::Color::Blue(), false);

            /*
            g.DrawCircle(position, GetTransform().GetForward(), GetTransform().GetUp(), size, rei::render::Color::Green(), 32);
            g.DrawWireSphere(position, size * 0.75, rei::render::Color(1,1,0,1), 32);
        */
        });
    }
};
