#pragma once
#include "Ecs/System.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"

namespace rei::editor
{
    class HandleTransformationControlsDragSystem : public ecs::System
    {
    public:
        explicit HandleTransformationControlsDragSystem(const std::shared_ptr<ecs::World>& ecsWorld);

        void OnUpdate() override;

    private:
        bool IsSnappingEnabled() const;
        f32 SnapValue(f32 value, f32 step) const;
        math::Vector3 SnapScaleDelta(const math::Vector3& scaleDelta, const math::Vector3& direction, f32 step) const;
        bool HasRectTransformTargets(const TransformationControl& control) const;
        void CaptureDragStartTargetStates(TransformationControl& control) const;
        const TransformationControlTargetState* FindDragStartTargetState(const TransformationControl& control, ecs::Entity entity) const;

        void ResetDragState(TransformationControl& control) const;
        bool HandleUiMovementDrag(TransformationControl& control) const;
        bool HandleUiScaleDrag(TransformationControl& control) const;
        bool HandleUiRotationDrag(TransformationControl& control) const;
        void HandleMovementDrag(TransformationControl& control) const;
        void HandleScaleDrag(TransformationControl& control) const;
        void HandleRotationDrag(TransformationControl& control) const;

    private:
        std::shared_ptr<ecs::Filter> _controlFilter;
    };
}
