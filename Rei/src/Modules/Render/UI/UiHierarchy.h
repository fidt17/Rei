#pragma once

#include <algorithm>
#include <unordered_map>
#include <vector>
#include "Common/Primitives.h"
#include "Ecs/Entity.h"

namespace rei::render
{
    // Per-collection snapshot: parenting changes appear on the next render.
    class UiHierarchy
    {
    public:
        struct Child
        {
            ecs::Entity Entity;
            i32 Order;
        };

        void AddChild(ecs::Entity entity, ecs::Entity parent, i32 order)
        {
            _children[parent].push_back({entity, order});
        }

        void Sort()
        {
            for (auto& [_, children] : _children)
            {
                std::ranges::sort(children, [](const Child& a, const Child& b) { return a.Order < b.Order; });
            }
        }

        const std::vector<Child>& GetChildren(ecs::Entity entity) const
        {
            static const std::vector<Child> empty;
            const auto found = _children.find(entity);
            return found != _children.end() ? found->second : empty;
        }

    private:
        std::unordered_map<ecs::Entity, std::vector<Child>> _children;
    };
}
