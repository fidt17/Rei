#pragma once
#include "TransformationControlMovementArrow.h"

namespace rei::editor
{
    struct TransformationControl
    {
        ecs::Entity TargetEntity = ecs::NULL_ENTITY;

        TransformationControlMovementArrow RightArrow = {};
        TransformationControlMovementArrow UpArrow = {};
        TransformationControlMovementArrow ForwardArrow = {};

        bool UseWorldSpace = true;
    };
}

EXPORT_COMPONENT(rei::editor::TransformationControl)
