#pragma once
#include "Ecs/System.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"

namespace rei::editor
{
    class UpdateTransformationControlsTransformsSystem : public ecs::System
    {
    public:
        explicit UpdateTransformationControlsTransformsSystem(const std::shared_ptr<ecs::World>& ecsWorld);

        void OnUpdate() override;

    private:
        void UpdateMovementArrow(const TransformationControl& control, const TransformationControlMovementArrow& arrow, const math::Vector3& targetPosition,
                                 const glm::quat& targetRotation, f32 controlScale) const;

        void UpdateMovementPlane(const TransformationControl& control, const TransformationControlMovementPlane& plane, const math::Vector3& targetPosition,
                                 const glm::quat& targetRotation, f32 controlScale) const;

        void UpdateScaleArrow(const ::rei::editor::TransformationControl& control, const ::rei::editor::TransformationControlScaleArrow& arrow, const ::rei::math::Vector3& targetPosition,
                              const glm::quat& targetRotation, f32 controlScale) const;

        void UpdateScaleRoot(const ::rei::editor::TransformationControl& control, const ::rei::editor::TransformationControlScaleArrow& root, const ::rei::math::Vector3& targetPosition,
                             const glm::quat& targetRotation, f32 controlScale) const;

        void UpdateRotationRing(const ::rei::editor::TransformationControl& control, const ::rei::editor::TransformationControlRotationRing& ring, const ::rei::math::Vector3& targetPosition,
                                const glm::quat& targetRotation, f32 controlScale) const;

    private:
        std::shared_ptr<ecs::Filter> _controlFilter;

    };
}
