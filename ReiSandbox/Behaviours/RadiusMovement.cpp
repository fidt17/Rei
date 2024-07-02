#include "RadiusMovement.h"

#include "rei_behaviours/render/light/PointLight.h"

void RadiusMovement::Update()
{

    const float RAD = 0.0174;
    _counter += .1f;

    auto& position = GetTransform().GetPosition();
    position.x = sin((_counter + _offset) * RAD) * 4;
    position.z = -cos((_counter + _offset) * RAD) * 4;
    //position.y = sin((_counter + _offset) * RAD);

    ECS_WORLD(rei::GetInternalWorld());
    auto& light = GET(GetEntity(), rei::behaviour::PointLight);
    light.SetStrength(sin(_counter + _offset) * RAD);
}
