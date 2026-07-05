#pragma once
#include <vector>

#include "TransformationControlMovementArrow.h"
#include "TransformationControlMovementPlane.h"
#include "TransformationControlRectHandle.h"
#include "TransformationControlRotationRing.h"
#include "TransformationControlScaleArrow.h"
#include "TransformationMode.h"
#include "TransformationControlTargetState.h"

namespace rei::editor
{
    struct TransformationControl
    {
        ecs::Entity PrimaryTargetEntity = ecs::NULL_ENTITY;
        std::vector<ecs::Entity> TargetEntities = {};
        std::vector<TransformationControlTargetState> DragStartTargetStates = {};
        math::Vector3 PivotWorldPosition = {};
        math::Vector2 PivotScreenPosition = {};
        bool HasRectTransformTargets = false;
        bool RectTransformBodyDragPending = false;
        bool RectTransformBodyDragActive = false;
        math::Vector3 RectTransformBodyDragStartPosition = {};

        TransformationControlMovementArrow RightMovementArrow = {};
        TransformationControlMovementArrow UpMovementArrow = {};
        TransformationControlMovementArrow ForwardMovementArrow = {};
        TransformationControlMovementPlane RightUpMovementPlane = {};
        TransformationControlMovementPlane RightForwardMovementPlane = {};
        TransformationControlMovementPlane UpForwardMovementPlane = {};

        TransformationControlScaleArrow RightScaleArrow = {};
        TransformationControlScaleArrow UpScaleArrow = {};
        TransformationControlScaleArrow ForwardScaleArrow = {};
        TransformationControlScaleArrow RootScale = {};

        TransformationControlRotationRing RightRotationRing = {};
        TransformationControlRotationRing UpRotationRing = {};
        TransformationControlRotationRing ForwardRotationRing = {};

        TransformationControlRectHandle TopLeftRectHandle = {};
        TransformationControlRectHandle TopRectHandle = {};
        TransformationControlRectHandle TopRightRectHandle = {};
        TransformationControlRectHandle LeftRectHandle = {};
        TransformationControlRectHandle RightRectHandle = {};
        TransformationControlRectHandle BottomLeftRectHandle = {};
        TransformationControlRectHandle BottomRectHandle = {};
        TransformationControlRectHandle BottomRightRectHandle = {};

        TransformationMode Mode = Movement;
        bool UseWorldSpace = false;

        bool HasTargets() const
        {
            return !TargetEntities.empty() && PrimaryTargetEntity != ecs::NULL_ENTITY;
        }

        bool IsUsingWorldSpace() const
        {
            return !HasRectTransformTargets && (UseWorldSpace || TargetEntities.size() > 1);
        }
    };
}

EXPORT_COMPONENT(rei::editor::TransformationControl)
