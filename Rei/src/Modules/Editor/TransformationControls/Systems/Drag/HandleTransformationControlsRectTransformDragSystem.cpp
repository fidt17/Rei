#include "pch.h"
#include "HandleTransformationControlsRectTransformDragSystem.h"

#include "TransformationControlDragUtilities.h"
#include "Common/Transform/RectTransformUtility.h"
#include "Modules/Editor/EditorPointerInteractionState.h"
#include "Modules/Input/Input.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/transformation/Transform.h"
#include "rei_behaviours/ui/Canvas.h"
#include "rei_behaviours/ui/RectTransform.h"

namespace rei::editor
{
    namespace drag = transformation_control_drag;

    namespace
    {
        constexpr f32 MIN_RECT_SIZE = 4.0f;
        constexpr f32 BODY_DRAG_THRESHOLD = 4.0f;

        f32 ClampSizeDeltaChange(const f32 currentSize, const f32 currentSizeDelta, const f32 startSizeDelta, const f32 desiredChange)
        {
            const f32 anchorSize = currentSize - currentSizeDelta;
            const f32 minChange = MIN_RECT_SIZE - anchorSize - startSizeDelta;
            return (std::max)(desiredChange, minChange);
        }
    }

    HandleTransformationControlsRectTransformDragSystem::HandleTransformationControlsRectTransformDragSystem(const std::shared_ptr<ecs::World>& ecsWorld) : System(ecsWorld)
    {
        _controlFilter = FILTER(TransformationControl);
    }

    void HandleTransformationControlsRectTransformDragSystem::OnUpdate()
    {
        if (!Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_LEFT)) return;

        const auto controlEntity = _controlFilter->First();
        if (IS_DEAD(controlEntity)) return;

        auto& control = GET(controlEntity, TransformationControl);
        if (control.Mode != RectTransform) return;

        HandleRectTransformDrag(control);
    }

    bool HandleTransformationControlsRectTransformDragSystem::HandleRectTransformDrag(TransformationControl& control) const
    {
        if (!drag::HasRectTransformTargets(_ecsWorld, control)) return false;

        if (control.TopLeftRectHandle.DragActive && TryHandleRectTransformHandleDrag(control, control.TopLeftRectHandle)) return true;
        if (control.TopRectHandle.DragActive && TryHandleRectTransformHandleDrag(control, control.TopRectHandle)) return true;
        if (control.TopRightRectHandle.DragActive && TryHandleRectTransformHandleDrag(control, control.TopRightRectHandle)) return true;
        if (control.LeftRectHandle.DragActive && TryHandleRectTransformHandleDrag(control, control.LeftRectHandle)) return true;
        if (control.RightRectHandle.DragActive && TryHandleRectTransformHandleDrag(control, control.RightRectHandle)) return true;
        if (control.BottomLeftRectHandle.DragActive && TryHandleRectTransformHandleDrag(control, control.BottomLeftRectHandle)) return true;
        if (control.BottomRectHandle.DragActive && TryHandleRectTransformHandleDrag(control, control.BottomRectHandle)) return true;
        if (control.BottomRightRectHandle.DragActive && TryHandleRectTransformHandleDrag(control, control.BottomRightRectHandle)) return true;

        if (TryHandleRectTransformHandleDrag(control, control.TopLeftRectHandle)) return true;
        if (TryHandleRectTransformHandleDrag(control, control.TopRectHandle)) return true;
        if (TryHandleRectTransformHandleDrag(control, control.TopRightRectHandle)) return true;
        if (TryHandleRectTransformHandleDrag(control, control.LeftRectHandle)) return true;
        if (TryHandleRectTransformHandleDrag(control, control.RightRectHandle)) return true;
        if (TryHandleRectTransformHandleDrag(control, control.BottomLeftRectHandle)) return true;
        if (TryHandleRectTransformHandleDrag(control, control.BottomRectHandle)) return true;
        if (TryHandleRectTransformHandleDrag(control, control.BottomRightRectHandle)) return true;

        return TryHandleRectTransformBodyDrag(control);
    }

    bool HandleTransformationControlsRectTransformDragSystem::TryHandleRectTransformHandleDrag(TransformationControl& control, TransformationControlRectHandle& handle) const
    {
        math::Vector3 pointerPos;
        Input::GetMousePosition(pointerPos.x, pointerPos.y);

        const auto& pointerListener = GET(handle.Entity, physics::PointerCollisionListener);
        if (!handle.DragActive && drag::ShouldStartPointerDrag(pointerListener))
        {
            handle.DragActive = true;
            handle.DragStartPosition = pointerPos;
            EditorPointerInteractionState::Consume();
            drag::CaptureDragStartTargetStates(_ecsWorld, control);
        }

        if (!handle.DragActive) return false;

        math::Vector2 delta = GetLogicalPointerDelta(control, pointerPos, handle.DragStartPosition);
        if (handle.Direction.x == 0.0f) delta.x = 0.0f;
        if (handle.Direction.y == 0.0f) delta.y = 0.0f;

        ApplyRectTransformDrag(control, handle, delta);
        return true;
    }

    bool HandleTransformationControlsRectTransformDragSystem::TryHandleRectTransformBodyDrag(TransformationControl& control) const
    {
        math::Vector3 pointerPos;
        Input::GetMousePosition(pointerPos.x, pointerPos.y);

        if (!control.RectTransformBodyDragPending && !control.RectTransformBodyDragActive && Input::IsMouseButtonPressed(GLFW_MOUSE_BUTTON_LEFT) && IsPointerInsidePrimaryTarget(control, pointerPos))
        {
            control.RectTransformBodyDragPending = true;
            control.RectTransformBodyDragStartPosition = pointerPos;
            return true;
        }

        if (control.RectTransformBodyDragPending && !control.RectTransformBodyDragActive)
        {
            const auto rawDelta = pointerPos - control.RectTransformBodyDragStartPosition;
            if (rawDelta.Length() < BODY_DRAG_THRESHOLD) return true;

            control.RectTransformBodyDragPending = false;
            control.RectTransformBodyDragActive = true;
            EditorPointerInteractionState::Consume();
            drag::CaptureDragStartTargetStates(_ecsWorld, control);
        }

        if (!control.RectTransformBodyDragActive) return false;

        if (!EditorPointerInteractionState::IsConsumed())
        {
            EditorPointerInteractionState::Consume();
            drag::CaptureDragStartTargetStates(_ecsWorld, control);
        }

        ApplyRectTransformMove(control, GetLogicalPointerDelta(control, pointerPos, control.RectTransformBodyDragStartPosition));
        return true;
    }

    bool HandleTransformationControlsRectTransformDragSystem::IsPointerInsidePrimaryTarget(const TransformationControl& control, const math::Vector3& pointerPosition) const
    {
        if (IS_DEAD(control.PrimaryTargetEntity) || !HAS(control.PrimaryTargetEntity, ui::RectTransform) || !HAS(control.PrimaryTargetEntity, Transform)) return false;

        const auto canvasEntity = ui_utility::FindCanvasEntity(control.PrimaryTargetEntity);
        if (IS_DEAD(canvasEntity) || !HAS(canvasEntity, ui::Canvas)) return false;

        const auto mainCamera = render::Camera::GetMainCamera();
        if (mainCamera.IsNull()) return false;

        i32 width = 1;
        i32 height = 1;
        mainCamera.Get().GetOutputSize(width, height);

        const auto& canvas = GET(canvasEntity, ui::Canvas);
        const f32 scaleFactor = ui_utility::CalculateCanvasScaleFactor(canvas, width, height);
        const auto logicalRect = ui_utility::CalculateRect(control.PrimaryTargetEntity, canvasEntity, width, height);
        const math::Rect pixelRect = {
            logicalRect.Min * scaleFactor,
            logicalRect.Max * scaleFactor
        };
        const math::Vector2 screenPoint(pointerPosition.x, static_cast<f32>(height) - pointerPosition.y);
        return ui_utility::IsScreenPointInside(screenPoint, pixelRect, GET(control.PrimaryTargetEntity, ui::RectTransform), GET(control.PrimaryTargetEntity, Transform));
    }

    math::Vector2 HandleTransformationControlsRectTransformDragSystem::GetLogicalPointerDelta(const TransformationControl& control, const math::Vector3& pointerPosition, const math::Vector3& dragStartPosition) const
    {
        const auto canvasEntity = ui_utility::FindCanvasEntity(control.PrimaryTargetEntity);
        if (IS_DEAD(canvasEntity) || !HAS(canvasEntity, ui::Canvas)) return {};

        const auto mainCamera = render::Camera::GetMainCamera();
        if (mainCamera.IsNull()) return {};

        i32 width = 1;
        i32 height = 1;
        mainCamera.Get().GetOutputSize(width, height);

        const f32 scaleFactor = ui_utility::CalculateCanvasScaleFactor(GET(canvasEntity, ui::Canvas), width, height);
        if (scaleFactor <= 0.0f) return {};

        math::Vector2 delta(
            (pointerPosition.x - dragStartPosition.x) / scaleFactor,
            -(pointerPosition.y - dragStartPosition.y) / scaleFactor);
        if (drag::IsSnappingEnabled())
        {
            delta.x = drag::SnapValue(delta.x, drag::MOVE_SNAP_STEP);
            delta.y = drag::SnapValue(delta.y, drag::MOVE_SNAP_STEP);
        }

        return delta;
    }

    void HandleTransformationControlsRectTransformDragSystem::ApplyRectTransformMove(TransformationControl& control, const math::Vector2& delta) const
    {
        for (const auto entity : control.TargetEntities)
        {
            if (IS_DEAD(entity) || !HAS(entity, ui::RectTransform)) continue;

            const auto* dragStartState = drag::FindDragStartTargetState(control, entity);
            if (dragStartState == nullptr) continue;

            GET(entity, ui::RectTransform).GetAnchoredPosition() = dragStartState->AnchoredPosition + delta;
        }
    }

    void HandleTransformationControlsRectTransformDragSystem::ApplyRectTransformDrag(TransformationControl& control, const TransformationControlRectHandle& handle, const math::Vector2& delta) const
    {
        for (const auto entity : control.TargetEntities)
        {
            if (IS_DEAD(entity) || !HAS(entity, ui::RectTransform)) continue;

            const auto* dragStartState = drag::FindDragStartTargetState(control, entity);
            if (dragStartState == nullptr) continue;

            auto& rectTransform = GET(entity, ui::RectTransform);
            const auto& pivot = rectTransform.GetPivot();

            const auto canvasEntity = ui_utility::FindCanvasEntity(entity);
            if (IS_DEAD(canvasEntity) || !HAS(canvasEntity, ui::Canvas)) continue;

            const auto mainCamera = render::Camera::GetMainCamera();
            if (mainCamera.IsNull()) continue;

            i32 width = 1;
            i32 height = 1;
            mainCamera.Get().GetOutputSize(width, height);

            const auto currentRect = ui_utility::CalculateRect(entity, canvasEntity, width, height);
            const auto currentSize = currentRect.GetSize();
            const auto currentSizeDelta = rectTransform.GetSizeDelta();

            const f32 minDeltaX = handle.Direction.x < 0.0f ? delta.x : 0.0f;
            const f32 maxDeltaX = handle.Direction.x > 0.0f ? delta.x : 0.0f;
            const f32 minDeltaY = handle.Direction.y < 0.0f ? delta.y : 0.0f;
            const f32 maxDeltaY = handle.Direction.y > 0.0f ? delta.y : 0.0f;

            math::Vector2 sizeDeltaChange(maxDeltaX - minDeltaX, maxDeltaY - minDeltaY);
            if (handle.Direction.x != 0.0f)
            {
                sizeDeltaChange.x = ClampSizeDeltaChange(currentSize.x, currentSizeDelta.x, dragStartState->SizeDelta.x, sizeDeltaChange.x);
            }
            if (handle.Direction.y != 0.0f)
            {
                sizeDeltaChange.y = ClampSizeDeltaChange(currentSize.y, currentSizeDelta.y, dragStartState->SizeDelta.y, sizeDeltaChange.y);
            }

            const math::Vector2 positionDelta(
                (handle.Direction.x < 0.0f ? -sizeDeltaChange.x : 0.0f) + pivot.x * sizeDeltaChange.x,
                (handle.Direction.y < 0.0f ? -sizeDeltaChange.y : 0.0f) + pivot.y * sizeDeltaChange.y);

            rectTransform.GetSizeDelta() = dragStartState->SizeDelta + sizeDeltaChange;
            rectTransform.GetAnchoredPosition() = dragStartState->AnchoredPosition + positionDelta;
        }
    }
}
