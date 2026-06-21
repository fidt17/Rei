#include "pch.h"

#include "UIUtility.h"

#include <algorithm>

#include "Engine/Services.h"
#include "rei_behaviours/transformation/Transform.h"
#include "rei_behaviours/ui/Button.h"
#include "rei_behaviours/ui/RectTransform.h"

namespace rei::render::ui_render_utility
{
    std::vector<i32> BuildHierarchySortKey(const ecs::Entity entity)
    {
        ECS_WORLD(rei::GetInternalWorld())

        std::vector<i32> key;
        auto current = entity;
        while (!IS_DEAD(current) && HAS(current, rei::Transform))
        {
            const auto& transform = GET(current, rei::Transform);
            key.push_back(transform.GetChildOrder());
            current = transform.GetParent();
        }

        std::reverse(key.begin(), key.end());
        return key;
    }

    ecs::Entity FindNearestButtonEntity(const ecs::Entity sourceEntity)
    {
        ECS_WORLD(GetInternalWorld())

        auto currentEntity = sourceEntity;
        while (!IS_DEAD(currentEntity))
        {
            if (HAS(currentEntity, ui::Button)) return currentEntity;
            if (!HAS(currentEntity, Transform)) return ecs::NULL_ENTITY;

            currentEntity = GET(currentEntity, Transform).GetParent();
        }

        return ecs::NULL_ENTITY;
    }

    bool IsUiEntity(const ecs::Entity entity)
    {
        ECS_WORLD(rei::GetInternalWorld())

        return HAS(entity, ui::RectTransform);
    }

    bool IsHigherUiEntity(const ecs::Entity candidate, const ecs::Entity current)
    {
        ECS_WORLD(rei::GetInternalWorld())

        if (IS_DEAD(current)) return true;

        return BuildHierarchySortKey(current) < BuildHierarchySortKey(candidate);
    }

    bool IsPointInsideRect(const math::Vector2& point, const math::Rect& rect)
    {
        return point.x >= rect.Min.x &&
               point.x <= rect.Max.x &&
               point.y >= rect.Min.y &&
               point.y <= rect.Max.y;
    }
}
