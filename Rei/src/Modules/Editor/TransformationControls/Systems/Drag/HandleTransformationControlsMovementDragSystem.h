#pragma once
#include "Ecs/System.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"

namespace rei::editor
{
    class HandleTransformationControlsMovementDragSystem : public ecs::System
    {
    public:
        explicit HandleTransformationControlsMovementDragSystem(const std::shared_ptr<ecs::World>& ecsWorld);

        void OnUpdate() override;

    private:
        bool HandleUiMovementDrag(TransformationControl& control) const;
        bool HandleUiMovementPlaneDrag(TransformationControl& control) const;
        bool HandleMovementPlaneDrag(TransformationControl& control) const;
        void HandleMovementDrag(TransformationControl& control) const;

    private:
        std::shared_ptr<ecs::Filter> _controlFilter;
    };
}
