#pragma once
#include "TransformationControl.h"
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Model/Model.h"
#include "rei_behaviours/render/camera/Camera.h"

namespace rei::editor
{
    class UpdateTransformationControlSystem : public ecs::System
    {
    public:
        UpdateTransformationControlSystem(const std::shared_ptr<ecs::EcsRegistry>& ecs, const std::shared_ptr<ecs::FilterProvider>& filters);

        void OnUpdate() override;

    private:
        void UpdateControlTarget(TransformationControl& tc) const;
        void UpdateControlTransform(const TransformationControl& tc) const;
        void UpdateControlRenderers(const TransformationControl& tc) const;
        void HandleControlDrag(TransformationControl& tc) const;

        void CreateTransformationControl();
        void SetupMovementArrow(TransformationControlMovementArrow& arrow, const math::Vector3& arrowDirection) const;

    private:
        ecs::Entity _transformationControl = ecs::NULL_ENTITY;
        std::shared_ptr<ecs::Filter> _selectedEntities;

        assets::AssetRef<render::Model> _arrowModel;
        assets::AssetRef<render::Material> _colorMaterial;

        ecs::RefComponent<render::Camera> _mainCamera;
    };
}
