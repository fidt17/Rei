#pragma once
#include "Modules/Render/Material/Material.h"
#include "Common/Math/Bounds.h"
#include "Modules/Render/Mesh/VertexObjects/CubeVertexData.h"
#include "Modules/Render/RenderScenario/CameraModule.h"
#include "Modules/Render/Shaders/Shader.h"
#include "rei_behaviours/render/light/AmbientLight.h"
#include "rei_behaviours/render/light/PointLight.h"

namespace rei::render
{
    class LightingRenderModule
    {
    private:
        struct PointLightSnapshot
        {
            math::Vector3 Position{};
            f32 Strength = 0;
            f32 Range = 0;
            Color LinearColor{0, 0, 0, 1};
        };
        static constexpr u32 LIGHT_UNIFORM_COUNT = 3 + REI_MAX_POINT_LIGHTS_COUNT * 4;
        using LightLocations = std::array<i32, LIGHT_UNIFORM_COUNT>;

    public:
        explicit LightingRenderModule(const std::shared_ptr<CameraModule>& cameraModule);

        void Setup();
        void OnBeforeRender();
        void Render() const;

        void SetLightValues(const Shader& shader, const math::Bounds& localBounds = {}, const glm::mat4& modelMatrix = glm::mat4(1)) const;

    private:
        void FindAmbientLights();
        void FindPointLights();
        void BuildSnapshot();
        const LightLocations& GetLightLocations(const Shader& shader) const;

    private:
        std::shared_ptr<CameraModule> _cameraModule;
        
        ecs::ComponentRef<AmbientLight> _ambientLight = {};
        std::vector<ecs::ComponentRef<PointLight>> _pointLights = {};
        i32 _pointLightLimit = REI_MAX_POINT_LIGHTS_COUNT;
        f32 _ambientStrength = 0;
        Color _ambientLinearColor{0, 0, 0, 1};
        std::vector<PointLightSnapshot> _pointSnapshot{};
        // Bounded to programs used this frame; locations themselves are cached by Shader.
        mutable std::unordered_map<u64, LightLocations> _lightLocations;

        CubeVertexData _cubeVertexData;

        assets::AssetRef<Material> _lightSourceMaterial{};

        void RenderPointLights() const;
    };
}
