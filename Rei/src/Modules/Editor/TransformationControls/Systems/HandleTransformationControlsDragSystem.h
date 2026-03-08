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

        void ResetDragState(TransformationControl& control) const;
        void HandleMovementDrag(TransformationControl& control) const;
        void HandleScaleDrag(TransformationControl& control) const;
        void HandleRotationDrag(TransformationControl& control) const;
        
    private:
        std::shared_ptr<ecs::Filter> _controlFilter;
    };
}
