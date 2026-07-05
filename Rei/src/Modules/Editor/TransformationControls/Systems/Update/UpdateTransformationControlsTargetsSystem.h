#pragma once
#include "Ecs/System.h"

namespace rei::editor
{
    class UpdateTransformationControlsTargetsSystem : public ecs::System
    {
    public:
        explicit UpdateTransformationControlsTargetsSystem(const std::shared_ptr<ecs::World>& world);

        void OnUpdate() override;

    private:
        std::shared_ptr<ecs::Filter> _controlFilter;
        std::shared_ptr<ecs::Filter> _selectedEntities;
    };
}
