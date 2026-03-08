#pragma once

#include "Ecs/System.h"

namespace rei::render
{
    class DebugOverlayToggleSystem final : public ecs::System
    {
    public:
        explicit DebugOverlayToggleSystem(const std::shared_ptr<ecs::World>& world) : System(world)
        {
        }

        void OnUpdate() override;
    };
}
