#include "pch.h"
#include "UpdateTransformationControlsRenderersSystem.h"

#include "Modules/Components/ActiveTag.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "Modules/Render/Color/Color.h"
#include "rei_behaviours/render/MeshRenderer.h"

namespace rei::editor
{
    UpdateTransformationControlsRenderersSystem::UpdateTransformationControlsRenderersSystem(const std::shared_ptr<ecs::World>& ecsWorld): System(ecsWorld)
    {
        _controlFilter = FILTER(TransformationControl);
    }

    void UpdateTransformationControlsRenderersSystem::OnUpdate()
    {
        const auto controlEntity = _controlFilter->First();
        if (IS_DEAD(controlEntity)) return;

        const auto& control = GET(controlEntity, TransformationControl);
        const bool hasTargets = control.HasTargets();
        const bool showDepthControls = !control.HasRectTransformTargets;

        UpdateMovementControls(control, hasTargets && control.Mode == Movement, showDepthControls);
        UpdateScaleControls(control, hasTargets && control.Mode == Scale, showDepthControls);
        UpdateRotationControls(control, hasTargets && control.Mode == Rotation, showDepthControls);
        UpdateRectTransformControls(control, hasTargets && control.Mode == RectTransform && control.HasRectTransformTargets);
    }

    void UpdateTransformationControlsRenderersSystem::UpdateMovementControls(const TransformationControl& control, const bool isVisible, const bool showDepthControls) const
    {
        const bool isRightUpPlaneDragging = control.RightUpMovementPlane.DragActive;
        const bool isRightForwardPlaneDragging = control.RightForwardMovementPlane.DragActive;
        const bool isUpForwardPlaneDragging = control.UpForwardMovementPlane.DragActive;
        const bool isPlaneDragging = isRightUpPlaneDragging || isRightForwardPlaneDragging || isUpForwardPlaneDragging;

        UpdateControlPart(control.RightMovementArrow.Entity, isVisible, red, redBright);
        UpdateControlPart(control.UpMovementArrow.Entity, isVisible, green, greenBright);
        UpdateControlPart(control.ForwardMovementArrow.Entity, isVisible && showDepthControls, blue, blueBright);
        UpdateControlPart(control.RightUpMovementPlane.Entity, isVisible && (!isPlaneDragging || isRightUpPlaneDragging), plane, planeBright);
        UpdateControlPart(control.RightForwardMovementPlane.Entity, isVisible && showDepthControls && (!isPlaneDragging || isRightForwardPlaneDragging), plane, planeBright);
        UpdateControlPart(control.UpForwardMovementPlane.Entity, isVisible && showDepthControls && (!isPlaneDragging || isUpForwardPlaneDragging), plane, planeBright);
    }

    void UpdateTransformationControlsRenderersSystem::UpdateScaleControls(const TransformationControl& control, const bool isVisible, const bool showDepthControls) const
    {
        UpdateControlPart(control.RightScaleArrow.Entity, isVisible, red, redBright);
        UpdateControlPart(control.UpScaleArrow.Entity, isVisible, green, greenBright);
        UpdateControlPart(control.ForwardScaleArrow.Entity, isVisible && showDepthControls, blue, blueBright);
        UpdateControlPart(control.RootScale.Entity, isVisible && showDepthControls, grey, greyBright);
    }

    void UpdateTransformationControlsRenderersSystem::UpdateRotationControls(const TransformationControl& control, const bool isVisible, const bool showDepthControls) const
    {
        UpdateControlPart(control.RightRotationRing.Entity, isVisible && showDepthControls, red, redBright);
        UpdateControlPart(control.UpRotationRing.Entity, isVisible && showDepthControls, green, greenBright);
        UpdateControlPart(control.ForwardRotationRing.Entity, isVisible, blue, blueBright);
    }

    void UpdateTransformationControlsRenderersSystem::UpdateRectTransformControls(const TransformationControl& control, const bool isVisible) const
    {
        UpdateControlPart(control.TopLeftRectHandle.Entity, isVisible, rectCorner, rectCornerBright);
        UpdateControlPart(control.TopRectHandle.Entity, isVisible, rectEdge, rectEdgeBright);
        UpdateControlPart(control.TopRightRectHandle.Entity, isVisible, rectCorner, rectCornerBright);
        UpdateControlPart(control.LeftRectHandle.Entity, isVisible, rectEdge, rectEdgeBright);
        UpdateControlPart(control.RightRectHandle.Entity, isVisible, rectEdge, rectEdgeBright);
        UpdateControlPart(control.BottomLeftRectHandle.Entity, isVisible, rectCorner, rectCornerBright);
        UpdateControlPart(control.BottomRectHandle.Entity, isVisible, rectEdge, rectEdgeBright);
        UpdateControlPart(control.BottomRightRectHandle.Entity, isVisible, rectCorner, rectCornerBright);
    }

    void UpdateTransformationControlsRenderersSystem::UpdateControlPart(const ecs::Entity& e, const bool visible, const render::Color& defaultColor, const render::Color& highlightColor) const
    {
        SetControlPartVisible(e, visible);
        if (visible) UpdateRenderer(e, defaultColor, highlightColor);
    }

    void UpdateTransformationControlsRenderersSystem::SetControlPartVisible(const ecs::Entity& e, const bool visible) const
    {
        if (IS_DEAD(e)) return;

        if (visible)
        {
            GET(e, ActiveTag);
            GET(e, render::MeshRenderer).Enable();
            return;
        }

        DEL(e, ActiveTag);
        GET(e, render::MeshRenderer).Disable();
        auto& listener = GET(e, physics::PointerCollisionListener);
        listener.DidEnter = false;
        listener.IsInside = false;
        listener.DidExit = false;
    }

    void UpdateTransformationControlsRenderersSystem::UpdateRenderer(const ecs::Entity& e, const render::Color& defaultColor,
                                                                     const render::Color& highlightColor) const
    {
        auto& meshRenderer = GET(e, render::MeshRenderer);

        const auto isPointerInside = GET(e, physics::PointerCollisionListener).IsInside;
        meshRenderer.GetMaterial()->GetShader().SetColor("_Color", isPointerInside ? highlightColor : defaultColor);
    }
}
