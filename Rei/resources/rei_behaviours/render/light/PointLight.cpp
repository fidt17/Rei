#include "pch.h"
#include "PointLight.h"

#include "Modules/Components/ActiveTag.h"
#include "Modules/Render/Lighting/LightGizmoGeometry.h"
#include "Modules/Render/Modules/Gizmos.h"
#include "Modules/Render/Lighting/LightUtility.h"

void rei::render::PointLight::OnGizmosSelected()
{
    ECS_WORLD(GetInternalWorld());
    const auto active = HAS(GetEntity(), ActiveTag) && IsEnabled();
    const auto shape = light_gizmo_geometry::Point(GetTransform().GetWorldPosition(), GetRange());
    GetGizmos().DrawSelectionLines(shape.Outer, Color(1, 0.82f, 0.15f, active ? 0.85f : 0.4f));
    GetGizmos().DrawSelectionLines(shape.Inner, Color(1, 0.82f, 0.15f, active ? 0.45f : 0.2f));
}

f32 rei::render::PointLight::GetStrength() const
{
    return _strength;
}

f32 rei::render::PointLight::GetRange() const
{
    return light_utility::ClampRange(_range);
}

rei::render::Color rei::render::PointLight::GetColor() const
{
    return _color;
}

void rei::render::PointLight::SetStrength(const f32 value)
{
    _strength = value;
}

void rei::render::PointLight::SetRange(const f32 value)
{
    _range = light_utility::ClampRange(value);
}

void rei::render::PointLight::SetColor(const Color value)
{
    _color = value;
}
