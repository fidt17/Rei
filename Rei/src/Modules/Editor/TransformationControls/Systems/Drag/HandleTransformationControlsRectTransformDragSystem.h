#pragma once
#include "Ecs/System.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"

namespace rei::editor
{
    class HandleTransformationControlsRectTransformDragSystem : public ecs::System
    {
    public:
        explicit HandleTransformationControlsRectTransformDragSystem(const std::shared_ptr<ecs::World>& ecsWorld);

        void OnUpdate() override;

    private:
        bool HandleRectTransformDrag(TransformationControl& control) const;
        bool TryHandleRectTransformHandleDrag(TransformationControl& control, TransformationControlRectHandle& handle) const;
        bool TryHandleRectTransformBodyDrag(TransformationControl& control) const;
        bool IsPointerInsidePrimaryTarget(const TransformationControl& control, const math::Vector3& pointerPosition) const;
        math::Vector2 GetLogicalPointerDelta(const TransformationControl& control, const math::Vector3& pointerPosition, const math::Vector3& dragStartPosition) const;
        void ApplyRectTransformMove(TransformationControl& control, const math::Vector2& delta) const;
        void ApplyRectTransformDrag(TransformationControl& control, const TransformationControlRectHandle& handle, const math::Vector2& delta) const;

    private:
        std::shared_ptr<ecs::Filter> _controlFilter;
    };
}
