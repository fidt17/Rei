#pragma once
#include <vector>

#include "TransformationControlMovementArrow.h"
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

        TransformationControlMovementArrow RightMovementArrow = {};
        TransformationControlMovementArrow UpMovementArrow = {};
        TransformationControlMovementArrow ForwardMovementArrow = {};

        TransformationControlScaleArrow RightScaleArrow = {};
        TransformationControlScaleArrow UpScaleArrow = {};
        TransformationControlScaleArrow ForwardScaleArrow = {};
        TransformationControlScaleArrow RootScale = {};

        TransformationControlRotationRing RightRotationRing = {};
        TransformationControlRotationRing UpRotationRing = {};
        TransformationControlRotationRing ForwardRotationRing = {};

        TransformationMode Mode = Movement;
        bool UseWorldSpace = false;

        bool HasTargets() const
        {
            return !TargetEntities.empty() && PrimaryTargetEntity != ecs::NULL_ENTITY;
        }

        bool IsUsingWorldSpace() const
        {
            return UseWorldSpace || TargetEntities.size() > 1;
        }
    };
}

EXPORT_COMPONENT(rei::editor::TransformationControl)
