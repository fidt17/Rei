#include "pch.h"
#include "UpdateTransformationControlsTargetsSystem.h"

#include "Common/Transform/RectTransformUtility.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/transformation/Transform.h"
#include "rei_behaviours/ui/Canvas.h"
#include "rei_behaviours/ui/RectTransform.h"

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
        transformationControl.PivotScreenPosition = {};
        transformationControl.HasRectTransformTargets = false;

        math::Vector3 pivotAccumulator = {};
        math::Vector2 screenPivotAccumulator = {};
        u32 selectedTargetsCount = 0;
        u32 screenTargetsCount = 0;

        const auto mainCamera = render::Camera::GetMainCamera();
        i32 screenWidth = 1;
        i32 screenHeight = 1;
        if (!mainCamera.IsNull())
        {
            mainCamera.Get().GetOutputSize(screenWidth, screenHeight);
        }

        FOR(selectedEntity, _selectedEntities)
        {
            if (IS_DEAD(selectedEntity) || !HAS(selectedEntity, Transform)) continue;

            transformationControl.TargetEntities.push_back(selectedEntity);
            if (IS_DEAD(transformationControl.PrimaryTargetEntity))
            {
                transformationControl.PrimaryTargetEntity = selectedEntity;
            }

            if (HAS(selectedEntity, ui::RectTransform))
            {
                const auto canvasEntity = ui_utility::FindCanvasEntity(selectedEntity);
                if (!IS_DEAD(canvasEntity) && HAS(canvasEntity, ui::Canvas))
                {
                    const auto& canvas = GET(canvasEntity, ui::Canvas);
                    const auto logicalRect = ui_utility::CalculateRect(selectedEntity, canvasEntity, screenWidth, screenHeight);
                    const f32 scaleFactor = ui_utility::CalculateCanvasScaleFactor(canvas, screenWidth, screenHeight);
                    const auto logicalPivot = ui_utility::GetPivotPosition(logicalRect, GET(selectedEntity, ui::RectTransform));
                    const auto pixelPivot = logicalPivot * scaleFactor;

                    screenPivotAccumulator += math::Vector2(pixelPivot.x, static_cast<f32>(screenHeight) - pixelPivot.y);
                    screenTargetsCount++;
                    transformationControl.HasRectTransformTargets = true;
                }
            }

            const auto worldPosition = GET(selectedEntity, Transform).GetWorldPosition();
            pivotAccumulator += worldPosition;
            if (!HAS(selectedEntity, ui::RectTransform) && !mainCamera.IsNull())
            {
                const auto screenPosition = mainCamera.Get().WorldToScreenPosition(worldPosition);
                screenPivotAccumulator += math::Vector2(screenPosition.x, screenPosition.y);
                screenTargetsCount++;
            }
            selectedTargetsCount++;
        }

        if (selectedTargetsCount == 0)
        {
            transformationControl.DragStartTargetStates.clear();
            return;
        }

        if (transformationControl.HasRectTransformTargets && !mainCamera.IsNull() && screenTargetsCount > 0)
        {
            transformationControl.UseWorldSpace = false;
            transformationControl.PivotScreenPosition = screenPivotAccumulator / static_cast<f32>(screenTargetsCount);
            const auto ray = mainCamera.Get().GetScreenPointToRay(transformationControl.PivotScreenPosition.x, transformationControl.PivotScreenPosition.y);
            transformationControl.PivotWorldPosition = ray.Origin + ray.Direction * 10.0f;
            return;
        }

        transformationControl.PivotWorldPosition = pivotAccumulator / static_cast<f32>(selectedTargetsCount);
        if (!mainCamera.IsNull())
        {
            const auto screenPosition = mainCamera.Get().WorldToScreenPosition(transformationControl.PivotWorldPosition);
            transformationControl.PivotScreenPosition = math::Vector2(screenPosition.x, screenPosition.y);
        }
    }
}
