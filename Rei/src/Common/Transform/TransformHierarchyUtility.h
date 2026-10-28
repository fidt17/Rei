#pragma once

#include <vector>

namespace rei
{
    class Transform;

    namespace transform_utility
    {
        REI_API std::vector<ecs::Entity> CollectSiblings(ecs::Entity parentEntity);
        i32 GetMaxOrderForParent(ecs::Entity parentEntity, ecs::Entity excludeEntity = ecs::NULL_ENTITY);
        void NormalizeSiblingOrders(ecs::Entity parentEntity, ecs::Entity excludeEntity = ecs::NULL_ENTITY);
        void InsertWithOrder(Transform& transform, ecs::Entity parent, i32 order);
        void MoveWithOrder(Transform& transform, ecs::Entity parent, i32 order);
        REI_API bool ReorderSiblings(const std::vector<ecs::Entity>& entities, i32 insertionIndex);
        REI_API bool ReparentEntities(const std::vector<ecs::Entity>& entities, ecs::Entity parent, i32 insertionIndex);
    }
}
