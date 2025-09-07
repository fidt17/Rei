#pragma once
#include "meshes/CubeVertexData.h"
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Shaders/Shader.h"
#include "rei_behaviours/render/light/AmbientLight.h"
#include "rei_behaviours/render/light/PointLight.h"

namespace rei::render
{
    class LightingRenderModule
    {
    public:
        void Setup();
        void OnBeforeRender(const glm::mat4& projectionMatrix, const glm::mat4& viewMatrix);
        void Render() const;

        void SetLightValues(const Shader& shader) const;

    private:
        void FindAmbientLights();
        void FindPointLights();

    private:
        glm::mat4 _projectionMatrix = 0;
        glm::mat4 _viewMatrix = 0;
        
        ecs::RefComponent<AmbientLight> _ambientLight = {};
        std::vector<ecs::RefComponent<PointLight>> _pointLights = {};

        CubeVertexData _cubeVertexData;

        assets::AssetRef<Material> _lightSourceMaterial{};

        void SetAmbientLight(const Shader& shader) const;
        void SetPointLights(const Shader& shader) const;
        void RenderPointLights() const;
    };
}
