#include "pch.h"
#include "TransformationControlsModule.h"

#include "Modules/Editor/TransformationControls/Systems/Activation/TransformationControlActivationSystem.h"
#include "Modules/Editor/TransformationControls/Systems/Creation/CreateTransformationControlsSystem.h"
#include "Modules/Editor/TransformationControls/Systems/Drag/HandleTransformationControlsMovementDragSystem.h"
#include "Modules/Editor/TransformationControls/Systems/Drag/HandleTransformationControlsRectTransformDragSystem.h"
#include "Modules/Editor/TransformationControls/Systems/Drag/HandleTransformationControlsRotationDragSystem.h"
#include "Modules/Editor/TransformationControls/Systems/Drag/HandleTransformationControlsScaleDragSystem.h"
#include "Modules/Editor/TransformationControls/Systems/Drag/ResetTransformationControlsDragSystem.h"
#include "Modules/Editor/TransformationControls/Systems/Update/UpdateTransformationControlsRenderersSystem.h"
#include "Modules/Editor/TransformationControls/Systems/Update/UpdateTransformationControlsTargetsSystem.h"
#include "Modules/Editor/TransformationControls/Systems/Update/UpdateTransformationControlsTransformsSystem.h"

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
        world->AddSystem<HandleTransformationControlsRectTransformDragSystem>();
        world->AddSystem<UpdateTransformationControlsTransformsSystem>();
        world->AddSystem<UpdateTransformationControlsRenderersSystem>();
    }
}
