#include "pch.h"

#include "TransformHierarchyUtility.h"

#include <algorithm>
#include <unordered_set>

#include "rei_behaviours/transformation/Transform.h"
#include "rei_behaviours/ui/RectTransform.h"
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

    void SortSiblings(std::vector<ecs::Entity>& siblings)
    {
        ECS_WORLD(GetInternalWorld())
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
    }

    void NormalizeSiblingOrders(const ecs::Entity parentEntity, const ecs::Entity excludeEntity)
    {
        ECS_WORLD(GetInternalWorld())

        auto siblings = CollectSiblings(parentEntity);
        siblings.erase(std::remove(siblings.begin(), siblings.end(), excludeEntity), siblings.end());
        SortSiblings(siblings);

        i32 index = 0;
        for (const auto sibling : siblings)
        {
            GET(sibling, Transform).SetChildOrder(index++);
        }
    }

    bool ReorderSiblings(const std::vector<ecs::Entity>& entities, const i32 insertionIndex)
    {
        if (entities.empty()) return false;
        ECS_WORLD(GetInternalWorld())
        std::unordered_set<ecs::Entity> selected;
        for (const auto entity : entities)
        {
            if (IS_DEAD(entity) || !HAS(entity, Transform) || !HAS(entity, EntityInfo)) return false;
            if (!selected.insert(entity).second) return false;
        }
        const auto parent = GET(entities.front(), Transform).GetParent();
        for (const auto entity : entities)
        {
            if (GET(entity, Transform).GetParent() != parent) return false;
        }
        auto siblings = CollectSiblings(parent);
        SortSiblings(siblings);
        const i32 insertion = std::clamp(insertionIndex, 0, static_cast<i32>(siblings.size()));
        i32 destination = insertion;
        std::vector<ecs::Entity> moved;
        std::vector<ecs::Entity> remaining;
        for (i32 index = 0; index < static_cast<i32>(siblings.size()); index++)
        {
            if (selected.contains(siblings[index]))
            {
                moved.push_back(siblings[index]);
                if (index < insertion) destination--;
            }
            else remaining.push_back(siblings[index]);
        }
        remaining.insert(remaining.begin() + destination, moved.begin(), moved.end());
        bool changed = false;
        for (i32 index = 0; index < static_cast<i32>(remaining.size()); index++)
        {
            auto& transform = GET(remaining[index], Transform);
            changed |= transform.GetChildOrder() != index;
            transform.SetChildOrder(index);
        }
        return changed;
    }

    bool CanReparentUnder(const ecs::Entity parent, const std::unordered_set<ecs::Entity>& selected)
    {
        ECS_WORLD(GetInternalWorld())
        for (auto ancestor = parent; ancestor != ecs::NULL_ENTITY; ancestor = GET(ancestor, Transform).GetParent())
        {
            if (IS_DEAD(ancestor) || !HAS(ancestor, Transform) || selected.contains(ancestor)) return false;
        }
        return true;
    }

    bool ReparentEntities(const std::vector<ecs::Entity>& entities, const ecs::Entity parent, const i32 insertionIndex, const bool allowRectTransforms)
    {
        if (entities.empty()) return false;
        ECS_WORLD(GetInternalWorld())
        std::unordered_set<ecs::Entity> selected;
        for (const auto entity : entities)
        {
            if (IS_DEAD(entity) || !HAS(entity, Transform) || !HAS(entity, EntityInfo)) return false;
            if (!allowRectTransforms && HAS(entity, ui::RectTransform)) return false;
            if (!selected.insert(entity).second) return false;
        }
        if (!CanReparentUnder(parent, selected)) return false;
        for (const auto entity : entities)
        {
            if (!CanReparentUnder(GET(entity, Transform).GetParent(), selected)) return false;
        }

        auto destination = CollectSiblings(parent);
        SortSiblings(destination);
        const i32 insertion = std::clamp(insertionIndex, 0, static_cast<i32>(destination.size()));
        i32 index = insertion;
        for (i32 i = 0; i < insertion; i++)
        {
            if (selected.contains(destination[i])) index--;
        }
        std::erase_if(destination, [&](const auto entity) { return selected.contains(entity); });
        destination.insert(destination.begin() + index, entities.begin(), entities.end());

        bool changed = false;
        for (const auto entity : entities)
        {
            auto& transform = GET(entity, Transform);
            if (transform.GetParent() == parent) continue;
            transform.SetParent(parent, GetMaxOrderForParent(parent) + 1);
            changed = true;
        }
        for (i32 i = 0; i < static_cast<i32>(destination.size()); i++)
        {
            auto& transform = GET(destination[i], Transform);
            changed |= transform.GetChildOrder() != i;
            transform.SetChildOrder(i);
        }
        return changed;
    }

    void InsertWithOrder(Transform& transform, const ecs::Entity parent, const i32 order)
    {
        ECS_WORLD(GetInternalWorld())

        const auto entity = transform.GetEntity();
        if (IS_DEAD(entity) || !HAS(entity, Transform)) return;

        NormalizeSiblingOrders(parent, entity);

        i32 newOrder = std::max(0, order);
        const i32 maxOrder = GetMaxOrderForParent(parent, entity);
        newOrder = std::min(newOrder, maxOrder + 1);

        IncrementOrdersFrom(parent, entity, newOrder);

        transform.SetParent(parent);
        transform.SetChildOrder(newOrder);
    }

    void MoveWithOrder(Transform& transform, const ecs::Entity parent, const i32 order)
    {
        ECS_WORLD(GetInternalWorld())

        const auto entity = transform.GetEntity();
        if (IS_DEAD(entity) || !HAS(entity, Transform)) return;

        const ecs::Entity oldParent = transform.GetParent();
        const auto worldPosition = transform.GetWorldPosition();
        const auto worldRotation = transform.GetWorldRotation();
        const auto worldScale = transform.GetWorldScale();

        if (oldParent == parent)
        {
            NormalizeSiblingOrders(parent);
            const i32 oldOrder = transform.GetChildOrder();
            const i32 newOrder = std::clamp(order, 0, GetMaxOrderForParent(parent));
            if (oldOrder == newOrder) return;

            ShiftSiblingOrdersWithinParent(parent, entity, oldOrder, newOrder);
            transform.SetChildOrder(newOrder);
            return;
        }

        NormalizeSiblingOrders(oldParent);
        NormalizeSiblingOrders(parent, entity);
        const i32 oldOrder = transform.GetChildOrder();
        i32 newOrder = std::max(0, order);

        DecrementOrdersAfter(oldParent, entity, oldOrder);
        const i32 maxOrder = GetMaxOrderForParent(parent, entity);

        newOrder = std::min(newOrder, maxOrder + 1);

        IncrementOrdersFrom(parent, entity, newOrder);

        transform.SetParent(parent);
        transform.SetChildOrder(newOrder);
        transform.SetWorldPosition(worldPosition);
        transform.SetWorldRotation(worldRotation);
        transform.SetWorldScale(worldScale);
    }
}
