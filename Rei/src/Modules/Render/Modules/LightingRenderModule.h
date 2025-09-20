#pragma once
#include "Modules/Render/Material/Material.h"
#include "Modules/Render/Mesh/VertexObjects/CubeVertexData.h"
#include "Modules/Render/RenderScenario/CameraModule.h"
#include "Modules/Render/Shaders/Shader.h"
#include "rei_behaviours/render/light/AmbientLight.h"
#include "rei_behaviours/render/light/PointLight.h"

namespace rei::render
{
    class LightingRenderModule
    {
    public:
        explicit LightingRenderModule(const std::shared_ptr<CameraModule>& cameraModule);

        void Setup();
        void OnBeforeRender();
        void Render() const;

        void SetLightValues(const Shader& shader) const;

    private:
        void FindAmbientLights();
        void FindPointLights();

    private:
        std::shared_ptr<CameraModule> _cameraModule;
        
        ecs::RefComponent<AmbientLight> _ambientLight = {};
        std::vector<ecs::RefComponent<PointLight>> _pointLights = {};

        CubeVertexData _cubeVertexData;

        assets::AssetRef<Material> _lightSourceMaterial{};

        void SetAmbientLight(const Shader& shader) const;
        void SetPointLights(const Shader& shader) const;
        void RenderPointLights() const;
    };
}
