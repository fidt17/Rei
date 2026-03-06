#include "pch.h"
#include "UpdateTransformationControlsTargetsSystem.h"

#include "Modules/Editor/TransformationControls/TransformationControl.h"

namespace rei::editor
{
    UpdateTransformationControlsTargetsSystem::UpdateTransformationControlsTargetsSystem(const std::shared_ptr<ecs::World>& world) : System(world)
    {
        _controlFilter = FILTER(TransformationControl);
        _selectedEntities = FILTER(Transform, SelectedTag);
    }

    void UpdateTransformationControlsTargetsSystem::OnUpdate()
    {
        const auto controlEntity = _controlFilter->First();
        if (IS_DEAD(controlEntity)) return;

        auto& transformationControl = GET(controlEntity, TransformationControl);

        const auto selectedEntity = FIND(Transform, SelectedTag)
        if (IS_ALIVE(selectedEntity))
        {
            transformationControl.TargetEntity = selectedEntity;
        }
        else
        {
            transformationControl.TargetEntity = ecs::NULL_ENTITY;
        }
    }
}
