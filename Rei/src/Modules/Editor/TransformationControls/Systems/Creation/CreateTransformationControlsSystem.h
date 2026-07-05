#pragma once
#include "Ecs/System.h"
#include "Modules/Editor/TransformationControls/TransformationControlMovementArrow.h"
#include "Modules/Editor/TransformationControls/TransformationControlMovementPlane.h"
#include "Modules/Editor/TransformationControls/TransformationControlRectHandle.h"
#include "Modules/Editor/TransformationControls/TransformationControlRotationRing.h"
#include "Modules/Editor/TransformationControls/TransformationControlScaleArrow.h"
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Model/Model.h"

namespace rei::editor
{
    class CreateTransformationControlsSystem : public ecs::System
    {
    public:
        CreateTransformationControlsSystem(const std::shared_ptr<ecs::World>& world);

        void OnUpdate() override { }

    private:
        
        void CreateTransformationControl() const;

        void CreateMovementArrow(TransformationControlMovementArrow& arrow, const math::Vector3& direction) const;
        void CreateMovementPlane(TransformationControlMovementPlane& plane, const math::Vector3& firstDirection, const math::Vector3& secondDirection) const;
        void CreateRotationRing(TransformationControlRotationRing& ring, const math::Vector3& direction) const;
        void CreateScaleArrow(TransformationControlScaleArrow& arrow, const math::Vector3& direction) const;
        void CreateScaleRoot(TransformationControlScaleArrow& arrow, const math::Vector3& direction) const;
        void CreateRectTransformHandle(TransformationControlRectHandle& handle, const math::Vector2& direction, bool isCorner) const;

    private:
        assets::AssetRef<render::Model> _scaleArrowModel;
        assets::AssetRef<render::Model> _cubeModel;
        assets::AssetRef<render::Model> _movementPlaneModel;
        assets::AssetRef<render::Model> _movementArrowModel;
        assets::AssetRef<render::Model> _rotationRingModel;
        assets::AssetRef<render::Material> _colorMaterial;
    };
}
