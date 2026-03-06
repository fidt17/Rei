#pragma once
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Mesh/VertexObjects/QuadVertexData.h"
#include "Modules/Render/RenderScenario/CameraModule.h"
#include "Modules/Render/RenderScenario/FrameBuffer.h"

namespace rei::render
{
    class PostProcessingModule
    {
    public:
        explicit PostProcessingModule(const std::shared_ptr<CameraModule>& cameraModule);

        void Setup();
        void Render(const FrameBuffer& frameBuffer) const;

    private:
        std::shared_ptr<CameraModule> _cameraModule;
        
        assets::AssetRef<Material> _overlayMaterial{};
        assets::AssetRef<Material> _grayscaleMaterial{};
        assets::AssetRef<Material> _inversionMaterial{};

        QuadVertexData _quadVertexData;
    };
}
