#pragma once
#include "Ecs/System.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"

namespace rei::render
{
    class Camera;
}

namespace rei::math
{
    struct Plane;
}

namespace rei::editor
{
    struct RectTransformControlBounds
    {
        math::Vector2 BottomLeft = {};
        math::Vector2 BottomRight = {};
        math::Vector2 TopLeft = {};
        math::Vector2 TopRight = {};
        i32 ScreenHeight = 1;
    };

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

        bool TryBuildRectTransformControlBounds(const TransformationControl& control, const render::Camera& camera, RectTransformControlBounds& bounds) const;

        void UpdateRectTransformHandle(const TransformationControlRectHandle& handle, const RectTransformControlBounds& bounds, const render::Camera& camera,
                                       const math::Plane& controlPlane, const glm::quat& cameraRotation, f32 controlScale) const;

        void UpdateRectTransformHandlePlaceholder(const TransformationControlRectHandle& handle, const math::Vector3& targetPosition,
                                       const glm::quat& cameraRotation, f32 controlScale) const;

    private:
        std::shared_ptr<ecs::Filter> _controlFilter;

    };
}
