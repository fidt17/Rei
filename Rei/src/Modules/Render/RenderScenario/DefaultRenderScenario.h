#pragma once
#include "BaseRenderScenario.h"
#include "FrameBuffer.h"
#include "../../../../resources/meshes/CubeVertexData.h"
#include "../../../../resources/meshes/QuadVertexData.h"
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Modules/BVHRenderModule.h"
#include "Modules/Render/Modules/GizmosModule.h"
#include "Modules/Render/Modules/LightingRenderModule.h"

namespace rei::render
{
    class Shader;
    class PointLight;
    class AmbientLight;

    class DefaultRenderScenario : public BaseRenderScenario
    {
    public:
        explicit DefaultRenderScenario(GLFWwindow* target);

        void Setup() override;
        void OnBeforeRender() override;
        void Render() override;
        void Dispose() override;

    private:
        void RenderInWireframeMode() const;
        void RenderInNormalMode();
        void RenderInDepthMode();
        
        void SetBackgroundColor(const Color& color) const;
        void ClearBuffer(int clearMask = GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT | GL_STENCIL_BUFFER_BIT, i32 stencilMask = 0xFF) const;

        void RenderMeshRenderers() const;
        void RenderMeshRenderersWithOverrideMaterial(const assets::AssetRef<Material>& material) const;
        void RenderOutlineObjects() const;

        void RenderOutlineFrame() const;
        void RenderPostprocessing() const;

    private:
        std::shared_ptr<GizmosModule> _gizmos;
        std::shared_ptr<BVHRenderModule> _bvh;
        std::shared_ptr<LightingRenderModule> _lighting;
        
        glm::mat4 _projectionMatrix = 0;
        glm::mat4 _viewMatrix = 0;
        i32 _outputWidth = 0, _outputHeight = 0;

        FrameBuffer _outlineObjectsBuffer;
        FrameBuffer _mainFrameBuffer;

        CubeVertexData _cubeVertexData;
        QuadVertexData _quadVertexData;

        assets::AssetRef<Material> _outlineQuadMaterial{};
        assets::AssetRef<Material> _depthMaterial{};
        
        assets::AssetRef<Material> _overlayMaterial{};
        assets::AssetRef<Material> _grayscaleMaterial{};
        assets::AssetRef<Material> _inversionMaterial{};
    };
}
