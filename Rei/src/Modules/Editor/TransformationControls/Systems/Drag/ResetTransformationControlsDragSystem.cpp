#include "pch.h"
#include "ResetTransformationControlsDragSystem.h"

#include "Modules/Input/Input.h"
#include "Modules/Editor/Actions/EditorActionStore.h"
#include "Modules/Editor/Actions/EntityMoveAction.h"
#include "Modules/Editor/Actions/RectTransformChangeAction.h"
#include "Modules/Editor/Actions/EntityScaleAction.h"
#include "Modules/Editor/Actions/EntityRotateAction.h"
#include "rei_behaviours/ui/RectTransform.h"

namespace rei::editor
{
    ResetTransformationControlsDragSystem::ResetTransformationControlsDragSystem(const std::shared_ptr<ecs::World>& ecsWorld, const std::shared_ptr<EditorActionStore>& actions) :
        System(ecsWorld), _actions(actions)
    {
        _controlFilter = FILTER(TransformationControl);
    }

    void ResetTransformationControlsDragSystem::OnUpdate()
    {
        if (Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_LEFT)) return;

        const auto controlEntity = _controlFilter->First();
        if (IS_DEAD(controlEntity)) return;

        auto& control = GET(controlEntity, TransformationControl);
        RecordTransformChange(control);
        ResetDragState(control);
    }

    void ResetTransformationControlsDragSystem::RecordTransformChange(const TransformationControl& control) const
    {
        if (control.DragStartTargetStates.empty()) return;
        for (const auto& start : control.DragStartTargetStates)
        {
            if (IS_DEAD(start.Entity) || !HAS(start.Entity, Transform)) return;
            if (HAS(start.Entity, ui::RectTransform))
            {
                RecordRectTransformChange(control);
                return;
            }
        }

        switch (control.Mode)
        {
        case Movement:
        {
            const bool dragging = control.RightMovementArrow.DragActive || control.UpMovementArrow.DragActive || control.ForwardMovementArrow.DragActive
                || control.RightUpMovementPlane.DragActive || control.RightForwardMovementPlane.DragActive || control.UpForwardMovementPlane.DragActive;
            if (!dragging) return;
            std::vector<EntityMoveAction::Change> changes;
            for (const auto& start : control.DragStartTargetStates)
            {
                const ecs::ComponentRef<Transform> transform(_ecs, start.Entity);
                const auto before = start.LocalPosition;
                const auto after = transform.Get().GetLocalPosition();
                if (glm::vec3(before) == glm::vec3(after)) continue;
                changes.push_back({transform, before, after});
            }
            if (!changes.empty()) _actions->RecordApplied(std::make_unique<EntityMoveAction>(std::move(changes)));
            return;
        }
        case Scale:
        {
            const bool dragging = control.RightScaleArrow.DragActive || control.UpScaleArrow.DragActive || control.ForwardScaleArrow.DragActive || control.RootScale.DragActive;
            if (!dragging) return;
            std::vector<EntityScaleAction::Change> changes;
            for (const auto& start : control.DragStartTargetStates)
            {
                const ecs::ComponentRef<Transform> transform(_ecs, start.Entity);
                const auto before = start.LocalScale;
                const auto after = transform.Get().GetLocalScale();
                if (glm::vec3(before) == glm::vec3(after)) continue;
                changes.push_back({transform, before, after});
            }
            if (!changes.empty()) _actions->RecordApplied(std::make_unique<EntityScaleAction>(std::move(changes)));
            return;
        }
        case Rotation:
        {
            const bool dragging = control.RightRotationRing.DragActive || control.UpRotationRing.DragActive || control.ForwardRotationRing.DragActive;
            if (!dragging) return;
            std::vector<EntityRotateAction::Change> changes;
            for (const auto& start : control.DragStartTargetStates)
            {
                const ecs::ComponentRef<Transform> transform(_ecs, start.Entity);
                const auto before = start.LocalRotation;
                const auto after = transform.Get().GetLocalRotation();
                if (before == after || before == -after) continue;
                changes.push_back({transform, before, after});
            }
            if (!changes.empty()) _actions->RecordApplied(std::make_unique<EntityRotateAction>(std::move(changes)));
            return;
        }
        }
    }

    void ResetTransformationControlsDragSystem::RecordRectTransformChange(const TransformationControl& control) const
    {
        if (control.DragStartTargetStates.size() != control.TargetEntities.size()) return;
        for (const auto& start : control.DragStartTargetStates)
        {
            if (IS_DEAD(start.Entity) || !HAS(start.Entity, ui::RectTransform)) return;
        }

        const bool resize = control.Mode == RectTransform && (control.TopLeftRectHandle.DragActive || control.TopRectHandle.DragActive
            || control.TopRightRectHandle.DragActive || control.LeftRectHandle.DragActive || control.RightRectHandle.DragActive
            || control.BottomLeftRectHandle.DragActive || control.BottomRectHandle.DragActive || control.BottomRightRectHandle.DragActive);
        const bool movement = control.Mode == Movement && (control.RightMovementArrow.DragActive || control.UpMovementArrow.DragActive || control.RightUpMovementPlane.DragActive);
        const bool body = control.Mode == RectTransform && control.RectTransformBodyDragActive;
        if (!resize && !movement && !body) return;

        std::vector<RectTransformChangeAction::Change> changes;
        for (const auto& start : control.DragStartTargetStates)
        {
            const ecs::ComponentRef<ui::RectTransform> target(_ecs, start.Entity);
            const auto after = RectTransformChangeAction::State::Capture(target.Get());
            if (!resize && glm::vec2(start.SizeDelta) != glm::vec2(after.SizeDelta)) return;
            auto before = after;
            before.AnchoredPosition = start.AnchoredPosition;
            before.SizeDelta = start.SizeDelta;
            changes.push_back({target, before, after});
        }
        const auto fields = resize ? RectTransformChangeAction::Fields::Rectangle : RectTransformChangeAction::Fields::Position;
        auto action = std::make_unique<RectTransformChangeAction>(fields, std::move(changes));
        if (action->HasChanges()) _actions->RecordApplied(std::move(action));
    }

    void ResetTransformationControlsDragSystem::ResetDragState(TransformationControl& control) const
    {
        control.RightMovementArrow.DragActive = false;
        control.UpMovementArrow.DragActive = false;
        control.ForwardMovementArrow.DragActive = false;
        control.RightUpMovementPlane.DragActive = false;
        control.RightForwardMovementPlane.DragActive = false;
        control.UpForwardMovementPlane.DragActive = false;

        control.RightScaleArrow.DragActive = false;
        control.UpScaleArrow.DragActive = false;
        control.ForwardScaleArrow.DragActive = false;
        control.RootScale.DragActive = false;

        control.RightScaleArrow.CurrentScaleMlt = 0;
        control.UpScaleArrow.CurrentScaleMlt = 0;
        control.ForwardScaleArrow.CurrentScaleMlt = 0;
        control.RootScale.CurrentScaleMlt = 0;

        control.RightRotationRing.DragActive = false;
        control.UpRotationRing.DragActive = false;
        control.ForwardRotationRing.DragActive = false;

        control.RightRotationRing.DragStartDirection = {};
        control.UpRotationRing.DragStartDirection = {};
        control.ForwardRotationRing.DragStartDirection = {};

        control.RightRotationRing.DragAxis = {};
        control.UpRotationRing.DragAxis = {};
        control.ForwardRotationRing.DragAxis = {};

        control.TopLeftRectHandle.DragActive = false;
        control.TopRectHandle.DragActive = false;
        control.TopRightRectHandle.DragActive = false;
        control.LeftRectHandle.DragActive = false;
        control.RightRectHandle.DragActive = false;
        control.BottomLeftRectHandle.DragActive = false;
        control.BottomRectHandle.DragActive = false;
        control.BottomRightRectHandle.DragActive = false;

        control.TopLeftRectHandle.DragStartPosition = {};
        control.TopRectHandle.DragStartPosition = {};
        control.TopRightRectHandle.DragStartPosition = {};
        control.LeftRectHandle.DragStartPosition = {};
        control.RightRectHandle.DragStartPosition = {};
        control.BottomLeftRectHandle.DragStartPosition = {};
        control.BottomRectHandle.DragStartPosition = {};
        control.BottomRightRectHandle.DragStartPosition = {};

        control.RectTransformBodyDragPending = false;
        control.RectTransformBodyDragActive = false;
        control.RectTransformBodyDragStartPosition = {};

        control.DragStartTargetStates.clear();
    }
}
