#pragma once
#include "BaseRenderScenario.h"
#include "../../../../resources/meshes/CubeVertexData.h"
#include "../../../../resources/rei_behaviours/render/Camera.h"

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
        void Render() override;
        void Dispose() override;

    private:
        void SetBackgroundColor() const;
        void SetPolygonMode() const;
        void ResetBuffers() const;

        void FindAmbientLights();
        void FindPointLights();

        void SetAmbientLight(const Shader& shader) const;
        void SetPointLights(const Shader& shader) const;

        void RenderMeshRenderers() const;
        void RenderOutlines() const;
        void RenderPointLights() const;

    private:
        glm::mat4 _projectionMatrix = 0;
        glm::mat4 _viewMatrix = 0;

        ecs::RefComponent<AmbientLight> _ambientLight = {};
        std::vector<ecs::RefComponent<PointLight>> _pointLights = {};

        CubeVertexData _cubeVertexData;
    };
}
