#pragma once
#include "Common/Math/Bounds.h"
#include "LightSnapshot.h"

namespace rei::render
{
    class LightSelector
    {
    public:
        struct Selection
        {
            math::Bounds LocalBounds{};
            glm::mat4 ModelMatrix{1};
            std::array<u64, REI_MAX_POINT_LIGHTS_COUNT> Indices{};
            u64 Revision = 0;
            u64 LastFrame = 0;
            i32 Limit = -1;
            i32 Count = 0;
        };

        void BeginFrame();
        const Selection& Select(const math::Bounds& localBounds, const glm::mat4& modelMatrix, ecs::Entity object, const LightSnapshot& snapshot) const;

    private:
        u64 _frame = 0;
        mutable Selection _uncachedSelection;
        // Keep objects drawn in this or previous frame. Entity keys include generation.
        mutable std::unordered_map<ecs::Entity, Selection> _selectionCache;
    };
}
