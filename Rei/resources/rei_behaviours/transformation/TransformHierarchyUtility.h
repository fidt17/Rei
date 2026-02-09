#pragma once

#include <vector>

namespace rei
{
    class Transform;

    namespace transform_utility
    {
        std::vector<ecs::Entity> CollectSiblings(ecs::Entity parentEntity);
        i32 GetMaxOrderForParent(ecs::Entity parentEntity, ecs::Entity excludeEntity = ecs::NULL_ENTITY);
        void NormalizeSiblingOrders(ecs::Entity parentEntity);
        void MoveWithOrder(Transform& transform, ecs::Entity parent, i32 order);
    }
}
