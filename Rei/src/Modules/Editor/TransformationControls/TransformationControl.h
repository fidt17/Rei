#pragma once
#include "TransformationControlMovementArrow.h"
#include "TransformationControlRotationRing.h"
#include "TransformationControlScaleArrow.h"
#include "TransformationMode.h"

namespace rei::editor
{
    struct TransformationControl
    {
        ecs::Entity TargetEntity = ecs::NULL_ENTITY;

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
    };
}

EXPORT_COMPONENT(rei::editor::TransformationControl)
