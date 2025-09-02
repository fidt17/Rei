#pragma once
#include "BaseRenderScenario.h"
#include "FrameBuffer.h"
#include "../../../../resources/meshes/CubeVertexData.h"
#include "../../../../resources/meshes/QuadVertexData.h"
#include "../../../../resources/rei_behaviours/render/Camera.h"
#include "Modules/Render/Material/Material.h"

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

        void SetBackgroundColor(const Color& color) const;
        void SetPolygonMode() const;
        void ClearBuffer(int clearMask = GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT | GL_STENCIL_BUFFER_BIT, i32 stencilMask = 0xFF) const;

        void FindAmbientLights();
        void FindPointLights();

        void SetAmbientLight(const Shader& shader) const;
        void SetPointLights(const Shader& shader) const;

        void RenderMeshRenderers() const;
        void RenderOutlineObjects() const;
        void RenderPointLights() const;
        
        void RenderOutlineFrame() const;

    private:
        glm::mat4 _projectionMatrix = 0;
        glm::mat4 _viewMatrix = 0;
        i32 _outputWidth = 0, _outputHeight = 0;

        FrameBuffer _outlineObjectsBuffer{};

        ecs::RefComponent<AmbientLight> _ambientLight = {};
        std::vector<ecs::RefComponent<PointLight>> _pointLights = {};

        CubeVertexData _cubeVertexData;
        QuadVertexData _quadVertexData;

        assets::AssetRef<Material> _lightSourceMaterial { };
        assets::AssetRef<Material> _outlineQuadMaterial { };
    };
}
