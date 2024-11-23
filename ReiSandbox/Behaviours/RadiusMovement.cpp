#include "RadiusMovement.h"

#include "rei_behaviours/render/light/PointLight.h"

void RadiusMovement::Update()
{
    auto& position = GetTransform().GetPosition();

    const float RAD = 0.0174;

    position.x = sin((_counter + _offset) * RAD) * 4;
    position.z = cos((_counter + _offset) * RAD) * 4;
    position.y = sin((_counter + _offset) * RAD);

    _counter += .1f;
}
