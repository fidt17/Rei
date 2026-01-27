#include "pch.h"
#include "UpdateTransformationControlsRenderersSystem.h"

#include "Modules/Editor/TransformationControls/TransformationControl.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "Modules/Render/Color/Color.h"
#include "rei_behaviours/render/MeshRenderer.h"

namespace rei::editor
{
    UpdateTransformationControlsRenderersSystem::UpdateTransformationControlsRenderersSystem(const std::shared_ptr<ecs::World>& ecsWorld): System(ecsWorld)
    {
        _controlFilter = FILTER(TransformationControl);
    }

    void UpdateTransformationControlsRenderersSystem::OnUpdate()
    {
        const auto controlEntity = _controlFilter->First();
        if (IS_DEAD(controlEntity)) return;

        const auto& control = GET(controlEntity, TransformationControl);

        UpdateRenderer(control.RightMovementArrow.Entity, red, redBright);
        UpdateRenderer(control.UpMovementArrow.Entity, green, greenBright);
        UpdateRenderer(control.ForwardMovementArrow.Entity, blue, blueBright);

        UpdateRenderer(control.RightScaleArrow.Entity, red, redBright);
        UpdateRenderer(control.UpScaleArrow.Entity, green, greenBright);
        UpdateRenderer(control.ForwardScaleArrow.Entity, blue, blueBright);
        UpdateRenderer(control.RootScale.Entity, grey, greyBright);
    }

    void UpdateTransformationControlsRenderersSystem::UpdateRenderer(const ecs::Entity& e, const render::Color& defaultColor,
                                                                     const render::Color& highlightColor) const
    {
        auto& meshRenderer = GET(e, render::MeshRenderer);

        const auto isPointerInside = GET(e, physics::PointerCollisionListener).IsInside;
        meshRenderer.GetMaterial()->GetShader().SetColor("_Color", isPointerInside ? highlightColor : defaultColor);
    }
}
