#pragma once
#include "BaseRenderScenario.h"
#include "CameraModule.h"
#include "FrameBuffer.h"
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Modules/BVHRenderModule.h"
#include "Modules/Render/Modules/Gizmos.h"
#include "Modules/Render/Modules/GridRenderModule.h"
#include "Modules/Render/Modules/LightingRenderModule.h"
#include "Modules/Render/Modules/DebugOverlayModule.h"
#include "Modules/Render/Modules/OutlineRenderModule.h"
#include "Modules/Render/Modules/PostProcessingModule.h"
#include "Modules/Render/Modules/UIRenderModule.h"

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
        void RenderInDepthMode() const;
        
        void SetBackgroundColor(const Color& color) const;
        void ClearBuffer(i32 clearMask = GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT | GL_STENCIL_BUFFER_BIT, i32 stencilMask = 0xFF) const;

        void RenderMeshRenderers(i32 minSortingOrder, i32 maxSortingOrder) const;
        void RenderMeshRenderersWithOverrideMaterial(const assets::AssetRef<Material>& material) const;
        void RenderSpriteRenderers(i32 minSortingOrder, i32 maxSortingOrder) const;
        void RenderSpriteRenderersWithOverrideMaterial(const assets::AssetRef<Material>& material) const;

    public:
        void SetCamera(const ecs::ComponentRef<Camera>& camera) override
        {
            BaseRenderScenario::SetCamera(camera);
            _cameraModule->SetCamera(camera);
        }

    private:
        std::shared_ptr<CameraModule> _cameraModule;
        std::shared_ptr<Gizmos> _gizmos;
        std::shared_ptr<BVHRenderModule> _bvh;
        std::shared_ptr<LightingRenderModule> _lighting;
        std::shared_ptr<OutlineRenderModule> _outline;
        std::shared_ptr<PostProcessingModule> _postProcessingModule;
        std::shared_ptr<GridRenderModule> _gridRenderModule;
        std::shared_ptr<DebugOverlayModule> _debugOverlayModule;
        std::shared_ptr<UIRenderModule> _uiRenderModule;

        FrameBuffer _mainFrameBuffer;

        assets::AssetRef<Material> _depthMaterial{};
    };
}
