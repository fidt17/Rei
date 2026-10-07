#include "pch.h"
#include "SpotLight.h"
#include "Modules/Components/ActiveTag.h"
#include "Modules/Render/Lighting/LightGizmoGeometry.h"
#include "Modules/Render/Modules/Gizmos.h"
#include "Modules/Render/Lighting/LightUtility.h"

void rei::render::SpotLight::OnGizmosSelected()
{
    ECS_WORLD(GetInternalWorld());
    const auto active = HAS(GetEntity(), ActiveTag) && IsEnabled();
    const auto shape = light_gizmo_geometry::Spot(GetTransform().GetWorldPosition(), light_utility::WorldRotation(GetTransform()), GetRange(), GetInnerAngle(), GetOuterAngle());
    GetGizmos().DrawSelectionLines(shape.Outer, Color(1, 0.82f, 0.15f, active ? 0.85f : 0.4f));
    GetGizmos().DrawSelectionLines(shape.Inner, Color(1, 0.82f, 0.15f, active ? 0.45f : 0.2f));
}

f32 rei::render::SpotLight::GetStrength() const { return _strength; }
f32 rei::render::SpotLight::GetRange() const { return light_utility::ClampRange(_range); }
rei::render::Color rei::render::SpotLight::GetColor() const { return _color; }
f32 rei::render::SpotLight::GetInnerAngle() const { return std::min(light_utility::ClampAngle(_innerAngle, 30), GetOuterAngle()); }
f32 rei::render::SpotLight::GetOuterAngle() const { return light_utility::ClampAngle(_outerAngle, 50); }
rei::math::Vector3 rei::render::SpotLight::GetWorldDirection() const { return light_utility::WorldRotation(GetTransform()) * glm::vec3(0, 0, 1); }

void rei::render::SpotLight::SetStrength(const f32 value) { _strength = value; }
void rei::render::SpotLight::SetRange(const f32 value) { _range = light_utility::ClampRange(value); }
void rei::render::SpotLight::SetColor(const Color value) { _color = value; }
void rei::render::SpotLight::SetInnerAngle(const f32 value) { _innerAngle = light_utility::ClampAngle(value, 30); }
void rei::render::SpotLight::SetOuterAngle(const f32 value) { _outerAngle = light_utility::ClampAngle(value, 50); }
