#pragma once
#include "Modules/Editor/Components/SelectedTag.h"
#include "rei_behaviours/render/RenderOutlineTag.h"

namespace rei::editor::selection_utility
{
    inline void Reset(const std::shared_ptr<ecs::World>& world)
    {
        ECS_WORLD(world);

        const auto& selectedEntities = FILTER(rei::editor::SelectedTag);
        FOR(e, selectedEntities)
        {
            DEL(e, rei::editor::SelectedTag);
            DEL(e, rei::render::RenderOutlineTag);
        }
    }

    inline void Select(const std::shared_ptr<ecs::World>& world, const ecs::Entity entity, const bool resetCurrentSelection = true)
    {
        ECS_WORLD(world);

        if (IS_DEAD(entity)) return;

        if (resetCurrentSelection)
        {
            Reset(world);
        }

        GET(entity, rei::editor::SelectedTag);
        GET(entity, rei::render::RenderOutlineTag);
    }
}
