#include "pch.h"

#include "TransformHierarchyUtility.h"

#include <algorithm>

#include "Transform.h"
#include "Modules/Components/EntityInfo.h"

namespace rei::transform_utility
{
    template <typename Action>
    void ForEachSibling(const ecs::Entity parentEntity, const ecs::Entity excludeEntity, Action&& action)
    {
        ECS_WORLD(GetInternalWorld())

        const auto& entityInfoFilter = FILTER(EntityInfo);
        FOR(other, entityInfoFilter)
        {
            if (other == excludeEntity || IS_DEAD(other) || !HAS(other, Transform) || !HAS(other, EntityInfo)) continue;

            auto& otherTransform = GET(other, Transform);
            if (otherTransform.GetParent() != parentEntity) continue;

            action(other, otherTransform);
        }
    }

    void ShiftSiblingOrdersWithinParent(const ecs::Entity parentEntity, const ecs::Entity excludeEntity,
                                        const i32 oldOrder, const i32 newOrder)
    {
        if (newOrder > oldOrder)
        {
            ForEachSibling(parentEntity, excludeEntity, [&](const ecs::Entity, Transform& otherTransform)
            {
                const i32 otherOrder = otherTransform.GetChildOrder();
                if (otherOrder > oldOrder && otherOrder <= newOrder)
                {
                    otherTransform.SetChildOrder(otherOrder - 1);
                }
            });
        }
        else
        {
            ForEachSibling(parentEntity, excludeEntity, [&](const ecs::Entity, Transform& otherTransform)
            {
                const i32 otherOrder = otherTransform.GetChildOrder();
                if (otherOrder >= newOrder && otherOrder < oldOrder)
                {
                    otherTransform.SetChildOrder(otherOrder + 1);
                }
            });
        }
    }

    void DecrementOrdersAfter(const ecs::Entity parentEntity, const ecs::Entity excludeEntity, const i32 order)
    {
        ForEachSibling(parentEntity, excludeEntity, [&](const ecs::Entity, Transform& otherTransform)
        {
            const i32 otherOrder = otherTransform.GetChildOrder();
            if (otherOrder > order)
            {
                otherTransform.SetChildOrder(otherOrder - 1);
            }
        });
    }

        void IncrementOrdersFrom(const ecs::Entity parentEntity, const ecs::Entity excludeEntity, const i32 order)
        {
            ForEachSibling(parentEntity, excludeEntity, [&](const ecs::Entity, Transform& otherTransform)
            {
                const i32 otherOrder = otherTransform.GetChildOrder();
            if (otherOrder >= order)
            {
                otherTransform.SetChildOrder(otherOrder + 1);
            }
        });
    }

    std::vector<ecs::Entity> CollectSiblings(const ecs::Entity parentEntity)
    {
        ECS_WORLD(GetInternalWorld())

        const auto& entityInfoFilter = FILTER(EntityInfo);
        std::vector<ecs::Entity> siblings;

        FOR(other, entityInfoFilter)
        {
            if (IS_DEAD(other) || !HAS(other, Transform) || !HAS(other, EntityInfo)) continue;

            const auto& otherTransform = GET(other, Transform);
            if (otherTransform.GetParent() != parentEntity) continue;

            siblings.push_back(other);
        }

        return siblings;
    }

    i32 GetMaxOrderForParent(const ecs::Entity parentEntity, const ecs::Entity excludeEntity)
    {
        i32 maxOrder = -1;
        ForEachSibling(parentEntity, excludeEntity, [&](const ecs::Entity, Transform& otherTransform)
        {
            maxOrder = std::max(maxOrder, otherTransform.GetChildOrder());
        });

        return maxOrder;
    }

    void NormalizeSiblingOrders(const ecs::Entity parentEntity)
    {
        ECS_WORLD(GetInternalWorld())

        auto siblings = CollectSiblings(parentEntity);
        std::ranges::sort(siblings, [&](const ecs::Entity& a, const ecs::Entity& b)
        {
            const auto& aTransform = GET(a, Transform);
            const auto& bTransform = GET(b, Transform);
            if (aTransform.GetChildOrder() == bTransform.GetChildOrder())
            {
                const auto& aInfo = GET(a, EntityInfo);
                const auto& bInfo = GET(b, EntityInfo);
                return aInfo.Id < bInfo.Id;
            }

            return aTransform.GetChildOrder() < bTransform.GetChildOrder();
        });

        i32 index = 0;
        for (const auto sibling : siblings)
        {
            GET(sibling, Transform).SetChildOrder(index++);
        }
    }

    void MoveWithOrder(Transform& transform, const ecs::Entity parent, const i32 order)
    {
        ECS_WORLD(GetInternalWorld())

        const auto entity = transform.GetEntity();
        if (IS_DEAD(entity) || !HAS(entity, Transform)) return;

        const ecs::Entity oldParent = transform.GetParent();

        NormalizeSiblingOrders(oldParent);
        if (oldParent != parent)
        {
            NormalizeSiblingOrders(parent);
        }

        const i32 oldOrder = transform.GetChildOrder();
        i32 newOrder = std::max(0, order);

        if (oldParent == parent && oldOrder == newOrder)
        {
            return;
        }

        if (oldParent == parent)
        {
            ShiftSiblingOrdersWithinParent(oldParent, entity, oldOrder, newOrder);

            transform.SetChildOrder(newOrder);
            return;
        }

        DecrementOrdersAfter(oldParent, entity, oldOrder);
        const i32 maxOrder = GetMaxOrderForParent(parent, entity);

        newOrder = std::min(newOrder, maxOrder + 1);

        IncrementOrdersFrom(parent, entity, newOrder);

        transform.SetParent(parent);
        transform.SetChildOrder(newOrder);
    }
}
