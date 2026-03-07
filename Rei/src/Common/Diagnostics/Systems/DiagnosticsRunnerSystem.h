#pragma once

#include "Ecs/System.h"

namespace rei::common::diagnostics
{
    class DiagnosticsRunnerSystem final : public ecs::System
    {
    public:
        explicit DiagnosticsRunnerSystem(const std::shared_ptr<ecs::World>& world) : System(world)
        {
        }

        void OnUpdate() override;
    };
}
