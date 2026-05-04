#include "pch.h"
#include "TransformationControlsModule.h"

#include "CreateTransformationControlsSystem.h"
#include "HandleTransformationControlsMovementDragSystem.h"
#include "HandleTransformationControlsRotationDragSystem.h"
#include "HandleTransformationControlsScaleDragSystem.h"
#include "ResetTransformationControlsDragSystem.h"
#include "TransformationControlActivationSystem.h"
#include "UpdateTransformationControlsRenderersSystem.h"
#include "UpdateTransformationControlsTargetsSystem.h"
#include "UpdateTransformationControlsTransformsSystem.h"

namespace rei::editor
{
    void TransformationControlsModule::AddSystems(const std::shared_ptr<ecs::World> world)
    {
        world->AddSystem<CreateTransformationControlsSystem>();
        world->AddSystem<UpdateTransformationControlsTargetsSystem>();
        world->AddSystem<TransformationControlActivationSystem>();
        world->AddSystem<ResetTransformationControlsDragSystem>();
        world->AddSystem<HandleTransformationControlsMovementDragSystem>();
        world->AddSystem<HandleTransformationControlsScaleDragSystem>();
        world->AddSystem<HandleTransformationControlsRotationDragSystem>();
        world->AddSystem<UpdateTransformationControlsTransformsSystem>();
        world->AddSystem<UpdateTransformationControlsRenderersSystem>();
    }
}
