#pragma once
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Mesh/VertexObjects/QuadVertexData.h"
#include "Modules/Render/RenderScenario/CameraModule.h"
#include "Modules/Render/RenderScenario/FrameBuffer.h"

namespace rei::render
{
    class OutlineRenderModule
    {
    public:
        explicit OutlineRenderModule(const std::shared_ptr<CameraModule>& cameraModule);

        void Setup();
        
        void RenderPass();
        void RenderOutlineFrame() const;
        
    private:
        void RenderOutlineObjects() const;

    private:
        std::shared_ptr<CameraModule> _cameraModule;
        
        FrameBuffer _outlineObjectsBuffer;
        QuadVertexData _quadVertexData;
        
        assets::AssetRef<Material> _outlineQuadMaterial{};
    };
}
