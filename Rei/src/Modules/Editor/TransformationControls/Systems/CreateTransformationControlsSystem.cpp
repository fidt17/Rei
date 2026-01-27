#include "pch.h"
#include "CreateTransformationControlsSystem.h"

#include "Modules/Editor/Components/SelectableByPointerTag.h"
#include "Modules/Editor/Components/SelectionByPointerBlockerTag.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "Modules/Render/Mesh/VertexObjects/ArrowVertexObject.h"
#include "Modules/Render/Mesh/VertexObjects/CubeVertexObject.h"
#include "rei_behaviours/render/MeshRenderer.h"
#include "rei_behaviours/transformation/Transform.h"

namespace rei::editor
{
    CreateTransformationControlsSystem::CreateTransformationControlsSystem(const std::shared_ptr<ecs::World>& world): System(world)
    {
        _movementArrowModel = GetAssetManager().CreateAsset<render::Model>("Movement Arrow",
                                                                           render::ArrowVertexObject(4, 0.65f, 0.2f, 0.05f).GenerateMesh());

        std::vector<render::Mesh> scaleArrowModelMeshes;
        scaleArrowModelMeshes.emplace_back(render::CubeVertexObject(math::Vector3(0, 0, 4), math::Vector3(0.3, 0.3, 0.3)).GenerateMesh()); // arrowTip
        scaleArrowModelMeshes.emplace_back(render::CubeVertexObject(math::Vector3(0, 0, 2), math::Vector3(0.05, 0.05, 4)).GenerateMesh()); // arrowLine
        _scaleArrowModel = GetAssetManager().CreateAsset<render::Model>("Scale arrow", scaleArrowModelMeshes);

        _cubeModel = GetAssetManager().CreateAsset<render::Model>("Cube", render::CubeVertexObject({0, 0, 0}, {1, 1, 1}).GenerateMesh());

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

        CreateScaleArrow(tc.ForwardScaleArrow, math::Vector3::Forward());
        CreateScaleArrow(tc.RightScaleArrow, math::Vector3::Right());
        CreateScaleArrow(tc.UpScaleArrow, math::Vector3::Up());
        CreateScaleRoot(tc.RootScale, math::Vector3(1, 1, 1));
    }

    void CreateTransformationControlsSystem::CreateMovementArrow(TransformationControlMovementArrow& arrow, const math::Vector3& direction) const
    {
        arrow.Direction = direction;

        arrow.Entity = NEW_ENTITY();
        GET(arrow.Entity, Transform).Reset();

        auto& meshRenderer = ADD_BEHAVIOUR(arrow.Entity, render::MeshRenderer);
        meshRenderer.SetModel(_movementArrowModel);

        auto arrowMaterial = render::Material::CreateInstanceFrom(*_colorMaterial.Asset);
        arrowMaterial->SetDepth(false);
        arrowMaterial->SetSortingOrder(SORTING_ORDER_POST_PROCESSING + 1);
        arrowMaterial->GetShader().SetColor("_Color", render::Color::White());
        meshRenderer.SetMaterial(arrowMaterial);

        GET(arrow.Entity, physics::PointerCollisionListener);
        DEL(arrow.Entity, SelectableByPointerTag);
        GET(arrow.Entity, SelectionByPointerBlockerTag);
    }

    void CreateTransformationControlsSystem::CreateScaleRoot(TransformationControlScaleArrow& arrow, const math::Vector3& direction) const
    {
        arrow.Direction = direction;

        arrow.Entity = NEW_ENTITY();
        GET(arrow.Entity, Transform).Reset();

        auto& meshRenderer = ADD_BEHAVIOUR(arrow.Entity, render::MeshRenderer);
        meshRenderer.SetModel(_cubeModel);

        auto arrowMaterial = render::Material::CreateInstanceFrom(*_colorMaterial.Asset);
        arrowMaterial->SetDepth(false);
        arrowMaterial->SetSortingOrder(SORTING_ORDER_POST_PROCESSING + 1);
        arrowMaterial->GetShader().SetColor("_Color", render::Color::White());
        meshRenderer.SetMaterial(arrowMaterial);

        GET(arrow.Entity, physics::PointerCollisionListener);
        DEL(arrow.Entity, SelectableByPointerTag);
        GET(arrow.Entity, SelectionByPointerBlockerTag);
    }

    void CreateTransformationControlsSystem::CreateScaleArrow(TransformationControlScaleArrow& arrow, const math::Vector3& direction) const
    {
        arrow.Direction = direction;

        arrow.Entity = NEW_ENTITY();
        GET(arrow.Entity, Transform).Reset();

        auto& meshRenderer = ADD_BEHAVIOUR(arrow.Entity, render::MeshRenderer);
        meshRenderer.SetModel(_scaleArrowModel);

        auto arrowMaterial = render::Material::CreateInstanceFrom(*_colorMaterial.Asset);
        arrowMaterial->SetDepth(false);
        arrowMaterial->SetSortingOrder(SORTING_ORDER_POST_PROCESSING + 1);
        arrowMaterial->GetShader().SetColor("_Color", render::Color::White());
        meshRenderer.SetMaterial(arrowMaterial);

        GET(arrow.Entity, physics::PointerCollisionListener);
        DEL(arrow.Entity, SelectableByPointerTag);
        GET(arrow.Entity, SelectionByPointerBlockerTag);
    }
}
