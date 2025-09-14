#pragma once
#include "meshes/ArrowVertexData.h"
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/RenderScenario/CameraModule.h"

namespace rei::render
{
    class TransformationControlsModule
    {
    public:
        explicit TransformationControlsModule(const std::shared_ptr<CameraModule>& cameraModule);

        void Setup();
        void DrawControls();

    private:
        void DrawMoveControls(transformation::Transform& transform) const;
        void DrawXArrow(const math::Vector3& pos) const;
        void DrawYArrow(const math::Vector3& pos) const;
        void DrawZArrow(const math::Vector3& pos) const;
        void DrawArrow(const math::Vector3& pos, const math::Vector3& dir, const Color& color) const;

    private:
        std::shared_ptr<CameraModule> _cameraModule;

        assets::AssetRef<Material> _arrowMaterial{};
        ArrowVertexData _arrow = ArrowVertexData(4, 0.65, 0.2, 0.05);
    };
}
