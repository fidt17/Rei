#include "pch.h"
#include "UpdateTransformationControlsTargetsSystem.h"

#include "Modules/Editor/TransformationControls/TransformationControl.h"
#include "rei_behaviours/transformation/Transform.h"

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
        transformationControl.TargetEntities.clear();
        transformationControl.PrimaryTargetEntity = ecs::NULL_ENTITY;
        transformationControl.PivotWorldPosition = {};

        math::Vector3 pivotAccumulator = {};
        u32 selectedTargetsCount = 0;

        FOR(selectedEntity, _selectedEntities)
        {
            if (IS_DEAD(selectedEntity) || !HAS(selectedEntity, Transform)) continue;

            transformationControl.TargetEntities.push_back(selectedEntity);
            if (IS_DEAD(transformationControl.PrimaryTargetEntity))
            {
                transformationControl.PrimaryTargetEntity = selectedEntity;
            }

            pivotAccumulator += GET(selectedEntity, Transform).GetWorldPosition();
            selectedTargetsCount++;
        }

        if (selectedTargetsCount == 0)
        {
            transformationControl.DragStartTargetStates.clear();
            return;
        }

        transformationControl.PivotWorldPosition = pivotAccumulator / static_cast<f32>(selectedTargetsCount);
    }
}
