#include "pch.h"
#include "UpdateTransformationControlSystem.h"

#include "Modules/Editor/Components/SelectableByPointerTag.h"
#include "Modules/Editor/Components/SelectionByPointerBlockerTag.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "Modules/Input/Input.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "Modules/Render/Mesh/VertexObjects/ArrowVertexObject.h"
#include "rei_behaviours/render/MeshRenderer.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/transformation/Transform.h"

namespace rei::editor
{
    UpdateTransformationControlSystem::UpdateTransformationControlSystem(const std::shared_ptr<ecs::EcsRegistry>& ecs,
                                                                         const std::shared_ptr<ecs::FilterProvider>& filters):
        System(ecs, filters),
        _selectedEntities(filters->Get<transformation::Transform, SelectedTag>())
    {
        _arrowModel = GetAssetManager().CreateAsset<render::Model>("Arrow", render::ArrowVertexObject(4, 0.65f, 0.2f, 0.05f).GenerateMesh());
        _colorMaterial = GetAssetManager().GetById<render::Material>(REI_COLOR_MATERIAL_ID);

        CreateTransformationControl();
    }

    void UpdateTransformationControlSystem::UpdateControlTarget(TransformationControl& tc) const
    {
        bool foundTarget = false;
        FOR(e, _selectedEntities)
        {
            tc.TargetEntity = e;
            foundTarget = true;
            break;
        }

        if (!foundTarget)
        {
            tc.TargetEntity = ecs::NULL_ENTITY;
        }
    }

    void UpdateTransformationControlSystem::UpdateControlTransform(const TransformationControl& tc) const
    {
        if (IS_DEAD(tc.TargetEntity)) return;

        auto& targetTransform = GET(tc.TargetEntity, transformation::Transform);
        const auto& targetPosition = targetTransform.GetPosition();
        const auto& targetRotation = targetTransform.GetRotation();

        const f32 controlScale = _mainCamera.Get().CalculateConstantScale(targetPosition, 0.5);

        auto updateArrow = [&](const TransformationControlMovementArrow& arrow)
        {
            const auto isPointerInside = GET(arrow.Entity, physics::PointerCollisionListener).IsInside;
            const auto scaleMlt = isPointerInside ? 1.01f : 1;

            auto& t = GET(arrow.Entity, transformation::Transform);
            t.GetPosition() = targetPosition;

            if (tc.UseWorldSpace)
            {
                t.SetRotation(LookAt(arrow.Direction, math::Vector3::Up()));
            }
            else
            {
                t.SetRotation(LookAt(arrow.Direction.Rotate(targetRotation), math::Vector3::Up()));
            }

            t.GetScale() = math::Vector3(controlScale, controlScale, controlScale) * scaleMlt;
        };
        updateArrow(tc.RightArrow);
        updateArrow(tc.UpArrow);
        updateArrow(tc.ForwardArrow);
    }

    void UpdateTransformationControlSystem::UpdateControlRenderers(const TransformationControl& tc) const
    {
        if (IS_DEAD(tc.TargetEntity))
        {
            GET(tc.RightArrow.Entity, render::MeshRenderer).Disable();
            GET(tc.UpArrow.Entity, render::MeshRenderer).Disable();
            GET(tc.ForwardArrow.Entity, render::MeshRenderer).Disable();
            return;
        }

        auto setArrowColor = [&](const TransformationControlMovementArrow& arrow, const render::Color& defaultColor, const render::Color& highlightColor)
        {
            auto& meshRenderer = GET(arrow.Entity, render::MeshRenderer);
            meshRenderer.Enable();

            const auto isPointerInside = GET(arrow.Entity, physics::PointerCollisionListener).IsInside;
            meshRenderer.GetMaterial()->GetShader().SetColor("_Color", isPointerInside ? highlightColor : defaultColor);
        };
        setArrowColor(tc.RightArrow, render::Color::FromHex("#bf212f"), render::Color::FromHex("#D52635")); // red
        setArrowColor(tc.UpArrow, render::Color::FromHex("#27b376"), render::Color::FromHex("#2FCE89")); // green
        setArrowColor(tc.ForwardArrow, render::Color::FromHex("#264b96"), render::Color::FromHex("#2E5BB4")); // blue
    }

    void UpdateTransformationControlSystem::HandleControlDrag(TransformationControl& tc) const
    {
        if (IS_DEAD(tc.TargetEntity)) return;

        const auto mainCamera = render::Camera::GetMainCamera();
        if (mainCamera.IsNull()) return;

        if (!Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_LEFT))
        {
            tc.RightArrow.DragActive = false;
            tc.UpArrow.DragActive = false;
            tc.ForwardArrow.DragActive = false;
            return;
        }

        math::Vector3 pointerPos;
        Input::GetMousePosition(pointerPos.x, pointerPos.y);

        auto tryMove = [&](TransformationControlMovementArrow& arrow) -> bool
        {
            auto& arrowTransform = GET(arrow.Entity, transformation::Transform);
            const auto& pointerListener = GET(arrow.Entity, physics::PointerCollisionListener);

            const auto& arrowPos = arrowTransform.GetPosition();
            const auto arrowForward = arrowTransform.GetForward();

            if (!arrow.DragActive && pointerListener.IsInside)
            {
                arrow.DragActive = true;
                arrow.DragStartPosition = arrowPos;

                arrow.DragPlane = math::Plane(arrowTransform.GetRight(), pointerListener.CollisionPoint);

                const math::Ray screenPointRay = mainCamera.Get().GetScreenPointToRay(pointerPos.x, pointerPos.y);
                math::Vector3 planeIntersectionPoint;
                PlaneRayIntersection(arrow.DragPlane, screenPointRay, planeIntersectionPoint);

                const math::Vector3 projectionOnArrowDirection = math::Vector3::Projection(planeIntersectionPoint - arrowPos, arrowForward);

                const auto arrowScale = arrowTransform.GetScale().x;
                arrow.DragOffset = projectionOnArrowDirection / arrowScale;
            }

            if (arrow.DragActive)
            {
                auto& targetTransform = GET(tc.TargetEntity, transformation::Transform);
                const auto offsetScaled = arrow.DragOffset * arrowTransform.GetScale();

                const math::Ray screenPointRay = mainCamera.Get().GetScreenPointToRay(pointerPos.x, pointerPos.y);
                math::Vector3 planeIntersectionPoint;
                PlaneRayIntersection(arrow.DragPlane, screenPointRay, planeIntersectionPoint);

                const math::Vector3 projectionOnArrowDirection = math::Vector3::Projection(planeIntersectionPoint - arrow.DragStartPosition, arrowForward);

                targetTransform.GetPosition() = arrow.DragStartPosition + projectionOnArrowDirection - offsetScaled;
            }

            return arrow.DragActive;
        };

        // allow movement only along 1 arrow at a time
        if (tc.RightArrow.DragActive && tryMove(tc.RightArrow)) return;
        if (tc.UpArrow.DragActive && tryMove(tc.UpArrow)) return;
        if (tc.ForwardArrow.DragActive && tryMove(tc.ForwardArrow)) return;

        if (tryMove(tc.RightArrow)) return;
        if (tryMove(tc.UpArrow)) return;
        if (tryMove(tc.ForwardArrow)) return;
    }

    void UpdateTransformationControlSystem::CreateTransformationControl()
    {
        ECS_WORLD(GetInternalWorld());

        _transformationControl = NEW_ENTITY();
        auto& tc = GET(_transformationControl, TransformationControl);
        SetupMovementArrow(tc.ForwardArrow, math::Vector3::Forward());
        SetupMovementArrow(tc.RightArrow, math::Vector3::Right());
        SetupMovementArrow(tc.UpArrow, math::Vector3::Up());
    }

    void UpdateTransformationControlSystem::SetupMovementArrow(TransformationControlMovementArrow& arrow, const math::Vector3& arrowDirection) const
    {
        arrow.Direction = arrowDirection;

        arrow.Entity = NEW_ENTITY();
        GET(arrow.Entity, transformation::Transform).Reset();

        auto& meshRenderer = ADD_BEHAVIOUR(arrow.Entity, render::MeshRenderer);
        meshRenderer.SetModel(_arrowModel);
        meshRenderer.Disable();

        auto arrowMaterial = render::Material::CreateInstanceFrom(*_colorMaterial.Asset);
        arrowMaterial->SetDepth(false);
        arrowMaterial->SetSortingOrder(SORTING_ORDER_POST_PROCESSING + 1);
        arrowMaterial->GetShader().SetColor("_Color", render::Color::White());
        meshRenderer.SetMaterial(arrowMaterial);

        GET(arrow.Entity, physics::PointerCollisionListener);
        DEL(arrow.Entity, SelectableByPointerTag);
        GET(arrow.Entity, SelectionByPointerBlockerTag);
    }

    void UpdateTransformationControlSystem::OnUpdate()
    {
        if (IS_DEAD(_transformationControl)) return;

        _mainCamera = render::Camera::GetMainCamera();
        if (_mainCamera.IsNull()) return;

        auto& tc = GET(_transformationControl, TransformationControl);

        UpdateControlTarget(tc);

        HandleControlDrag(tc);
        UpdateControlTransform(tc);

        UpdateControlRenderers(tc);
    }
}
