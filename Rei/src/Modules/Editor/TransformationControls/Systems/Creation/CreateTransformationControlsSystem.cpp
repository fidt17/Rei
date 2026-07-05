#include "pch.h"
#include "CreateTransformationControlsSystem.h"

#include "Modules/Editor/Components/SelectableByPointerTag.h"
#include "Modules/Editor/Components/SelectionByPointerBlockerTag.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "Modules/Render/Mesh/VertexObjects/ArrowVertexObject.h"
#include "Modules/Render/Mesh/VertexObjects/CubeVertexObject.h"
#include "Modules/Render/Mesh/VertexObjects/RingVertexObject.h"
#include "rei_behaviours/render/MeshRenderer.h"
#include "rei_behaviours/transformation/Transform.h"

namespace rei::editor
{
    CreateTransformationControlsSystem::CreateTransformationControlsSystem(const std::shared_ptr<ecs::World>& world): System(world)
    {
        _movementArrowModel = GetAssetManager().CreateAsset<render::Model>("Movement Arrow",
                                                                           render::ArrowVertexObject(4, 0.65f, 0.2f, 0.05f).GenerateMesh());

        std::vector<render::Mesh> scaleArrowModelMeshes;
        scaleArrowModelMeshes.emplace_back(render::CubeVertexObject(math::Vector3(0, 0, 4), math::Vector3(0.3f, 0.3f, 0.3f)).GenerateMesh()); // arrowTip
        scaleArrowModelMeshes.emplace_back(render::CubeVertexObject(math::Vector3(0, 0, 2), math::Vector3(0.05f, 0.05f, 4)).GenerateMesh()); // arrowLine
        _scaleArrowModel = GetAssetManager().CreateAsset<render::Model>("Scale arrow", scaleArrowModelMeshes);

        _cubeModel = GetAssetManager().CreateAsset<render::Model>("Cube", render::CubeVertexObject({0, 0, 0}, {1, 1, 1}).GenerateMesh());
        constexpr f32 MOVEMENT_PLANE_MARGIN = 0.22f;
        constexpr f32 MOVEMENT_PLANE_SIZE = 0.62f;
        constexpr f32 MOVEMENT_PLANE_THICKNESS = 0.06f;
        const auto movementPlaneCenter = math::Vector3(MOVEMENT_PLANE_MARGIN + MOVEMENT_PLANE_SIZE * 0.5f, MOVEMENT_PLANE_MARGIN + MOVEMENT_PLANE_SIZE * 0.5f, 0);
        _movementPlaneModel = GetAssetManager().CreateAsset<render::Model>("Movement Plane", render::CubeVertexObject(movementPlaneCenter, {MOVEMENT_PLANE_SIZE, MOVEMENT_PLANE_SIZE, MOVEMENT_PLANE_THICKNESS}).GenerateMesh());

        _rotationRingModel = GetAssetManager().CreateAsset<render::Model>("Rotation ring", render::RingVertexObject(4.2f, 0.2f, 64).GenerateMesh());

        _colorMaterial = GetAssetManager().GetById<render::Material>(REI_COLOR_MATERIAL_ID);

        CreateTransformationControl();
    }

    void CreateTransformationControlsSystem::CreateTransformationControl() const
    {
        ECS_WORLD(GetInternalWorld());

        const auto transformationControl = NEW_ENTITY();
        auto& tc = GET(transformationControl, TransformationControl);

        CreateMovementArrow(tc.ForwardMovementArrow, math::Vector3::Forward());
        CreateMovementArrow(tc.RightMovementArrow, math::Vector3::Right());
        CreateMovementArrow(tc.UpMovementArrow, math::Vector3::Up());
        CreateMovementPlane(tc.RightUpMovementPlane, math::Vector3::Right(), math::Vector3::Up());
        CreateMovementPlane(tc.RightForwardMovementPlane, math::Vector3::Right(), math::Vector3::Forward());
        CreateMovementPlane(tc.UpForwardMovementPlane, math::Vector3::Up(), math::Vector3::Forward());

        CreateScaleArrow(tc.ForwardScaleArrow, math::Vector3::Forward());
        CreateScaleArrow(tc.RightScaleArrow, math::Vector3::Right());
        CreateScaleArrow(tc.UpScaleArrow, math::Vector3::Up());
        CreateScaleRoot(tc.RootScale, math::Vector3(1, 1, 1));

        CreateRotationRing(tc.ForwardRotationRing, math::Vector3::Forward());
        CreateRotationRing(tc.RightRotationRing, math::Vector3::Right());
        CreateRotationRing(tc.UpRotationRing, math::Vector3::Up());

        CreateRectTransformHandle(tc.TopLeftRectHandle, math::Vector2(-1, 1), true);
        CreateRectTransformHandle(tc.TopRectHandle, math::Vector2(0, 1), false);
        CreateRectTransformHandle(tc.TopRightRectHandle, math::Vector2(1, 1), true);
        CreateRectTransformHandle(tc.LeftRectHandle, math::Vector2(-1, 0), false);
        CreateRectTransformHandle(tc.RightRectHandle, math::Vector2(1, 0), false);
        CreateRectTransformHandle(tc.BottomLeftRectHandle, math::Vector2(-1, -1), true);
        CreateRectTransformHandle(tc.BottomRectHandle, math::Vector2(0, -1), false);
        CreateRectTransformHandle(tc.BottomRightRectHandle, math::Vector2(1, -1), true);
    }

    void CreateTransformationControlsSystem::CreateMovementArrow(TransformationControlMovementArrow& arrow, const math::Vector3& direction) const
    {
        arrow.Direction = direction;

        arrow.Entity = NEW_ENTITY();
        GET(arrow.Entity, Transform).Reset();

        auto& meshRenderer = ADD_BEHAVIOUR(arrow.Entity, render::MeshRenderer);
        meshRenderer.SetModel(_movementArrowModel);

        auto arrowMaterial = render::Material::CreateInstanceFrom(*_colorMaterial.Get());
        arrowMaterial->SetDepth(false);
        arrowMaterial->SetSortingOrder(SORTING_ORDER_POST_PROCESSING + 1);
        arrowMaterial->GetShader().SetColor("_Color", render::Color::White());
        meshRenderer.SetMaterial(arrowMaterial);

        GET(arrow.Entity, physics::PointerCollisionListener);
        DEL(arrow.Entity, SelectableByPointerTag);
        GET(arrow.Entity, SelectionByPointerBlockerTag);
    }

    void CreateTransformationControlsSystem::CreateMovementPlane(TransformationControlMovementPlane& plane, const math::Vector3& firstDirection, const math::Vector3& secondDirection) const
    {
        plane.FirstDirection = firstDirection;
        plane.SecondDirection = secondDirection;

        plane.Entity = NEW_ENTITY();
        GET(plane.Entity, Transform).Reset();

        auto& meshRenderer = ADD_BEHAVIOUR(plane.Entity, render::MeshRenderer);
        meshRenderer.SetModel(_movementPlaneModel);

        auto planeMaterial = render::Material::CreateInstanceFrom(*_colorMaterial.Get());
        planeMaterial->SetDepth(false);
        planeMaterial->SetSortingOrder(SORTING_ORDER_POST_PROCESSING + 1);
        planeMaterial->GetShader().SetColor("_Color", render::Color(1.0f, 1.0f, 1.0f, 0.25f));
        meshRenderer.SetMaterial(planeMaterial);

        GET(plane.Entity, physics::PointerCollisionListener);
        DEL(plane.Entity, SelectableByPointerTag);
        GET(plane.Entity, SelectionByPointerBlockerTag);
    }

    void CreateTransformationControlsSystem::CreateScaleRoot(TransformationControlScaleArrow& arrow, const math::Vector3& direction) const
    {
        arrow.Direction = direction;

        arrow.Entity = NEW_ENTITY();
        GET(arrow.Entity, Transform).Reset();

        auto& meshRenderer = ADD_BEHAVIOUR(arrow.Entity, render::MeshRenderer);
        meshRenderer.SetModel(_cubeModel);

        auto arrowMaterial = render::Material::CreateInstanceFrom(*_colorMaterial.Get());
        arrowMaterial->SetDepth(false);
        arrowMaterial->SetSortingOrder(SORTING_ORDER_POST_PROCESSING + 1);
        arrowMaterial->GetShader().SetColor("_Color", render::Color::White());
        meshRenderer.SetMaterial(arrowMaterial);

        GET(arrow.Entity, physics::PointerCollisionListener);
        DEL(arrow.Entity, SelectableByPointerTag);
        GET(arrow.Entity, SelectionByPointerBlockerTag);
    }

    void CreateTransformationControlsSystem::CreateRotationRing(TransformationControlRotationRing& ring, const math::Vector3& direction) const
    {
        ring.Direction = direction;

        ring.Entity = NEW_ENTITY();
        GET(ring.Entity, Transform).Reset();

        auto& meshRenderer = ADD_BEHAVIOUR(ring.Entity, render::MeshRenderer);
        meshRenderer.SetModel(_rotationRingModel);

        auto ringMaterial = render::Material::CreateInstanceFrom(*_colorMaterial.Get());
        ringMaterial->SetDepth(false);
        ringMaterial->SetSortingOrder(SORTING_ORDER_POST_PROCESSING + 1);
        ringMaterial->GetShader().SetColor("_Color", render::Color::White());
        meshRenderer.SetMaterial(ringMaterial);

        GET(ring.Entity, physics::PointerCollisionListener);
        DEL(ring.Entity, SelectableByPointerTag);
        GET(ring.Entity, SelectionByPointerBlockerTag);
    }

    void CreateTransformationControlsSystem::CreateScaleArrow(TransformationControlScaleArrow& arrow, const math::Vector3& direction) const
    {
        arrow.Direction = direction;

        arrow.Entity = NEW_ENTITY();
        GET(arrow.Entity, Transform).Reset();

        auto& meshRenderer = ADD_BEHAVIOUR(arrow.Entity, render::MeshRenderer);
        meshRenderer.SetModel(_scaleArrowModel);

        auto arrowMaterial = render::Material::CreateInstanceFrom(*_colorMaterial.Get());
        arrowMaterial->SetDepth(false);
        arrowMaterial->SetSortingOrder(SORTING_ORDER_POST_PROCESSING + 1);
        arrowMaterial->GetShader().SetColor("_Color", render::Color::White());
        meshRenderer.SetMaterial(arrowMaterial);

        GET(arrow.Entity, physics::PointerCollisionListener);
        DEL(arrow.Entity, SelectableByPointerTag);
        GET(arrow.Entity, SelectionByPointerBlockerTag);
    }

    void CreateTransformationControlsSystem::CreateRectTransformHandle(TransformationControlRectHandle& handle, const math::Vector2& direction, const bool isCorner) const
    {
        handle.Direction = direction;
        handle.IsCorner = isCorner;

        handle.Entity = NEW_ENTITY();
        GET(handle.Entity, Transform).Reset();

        auto& meshRenderer = ADD_BEHAVIOUR(handle.Entity, render::MeshRenderer);
        meshRenderer.SetModel(_cubeModel);

        auto handleMaterial = render::Material::CreateInstanceFrom(*_colorMaterial.Get());
        handleMaterial->SetDepth(false);
        handleMaterial->SetSortingOrder(SORTING_ORDER_POST_PROCESSING + 1);
        handleMaterial->GetShader().SetColor("_Color", render::Color::White());
        meshRenderer.SetMaterial(handleMaterial);

        GET(handle.Entity, physics::PointerCollisionListener);
        DEL(handle.Entity, SelectableByPointerTag);
        GET(handle.Entity, SelectionByPointerBlockerTag);
    }
}
