#pragma once

#include <vector>

#include "Common/Math/Rect.h"
#include "Ecs/Entity.h"

namespace rei::render::ui_render_utility
{
    std::vector<i32> BuildHierarchySortKey(ecs::Entity entity);
    ecs::Entity FindNearestButtonEntity(ecs::Entity sourceEntity);
    bool IsUiEntity(ecs::Entity entity);
    bool IsHigherUiEntity(ecs::Entity candidate, ecs::Entity current);
    bool IsPointInsideRect(const math::Vector2& point, const math::Rect& rect);
}
