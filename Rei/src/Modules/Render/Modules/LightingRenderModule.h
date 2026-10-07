#pragma once
#include "Common/Math/Bounds.h"
#include "Modules/Render/RenderScenario/CameraModule.h"
#include "Modules/Render/Shaders/Shader.h"
#include "Modules/Render/Lighting/LightSnapshot.h"
#include "Modules/Render/Lighting/LightSelector.h"

namespace rei::render
{
    class LightingRenderModule
    {
    private:
        static constexpr u32 LIGHT_UNIFORM_COUNT = 3 + REI_MAX_POINT_LIGHTS_COUNT * 4;
        using LightLocations = std::array<i32, LIGHT_UNIFORM_COUNT>;

    public:
        explicit LightingRenderModule(const std::shared_ptr<CameraModule>& cameraModule);

        void OnBeforeRender();

        void SetLightValues(const Shader& shader, const math::Bounds& localBounds = {}, const glm::mat4& modelMatrix = glm::mat4(1), ecs::Entity object = ecs::NULL_ENTITY) const;

    private:
        const LightLocations& GetLightLocations(const Shader& shader) const;

    private:
        std::shared_ptr<CameraModule> _cameraModule;
        
        LightSnapshot _snapshot;
        LightSelector _selector;
        // Bounded to programs used this frame; locations themselves are cached by Shader.
        mutable std::unordered_map<u64, LightLocations> _lightLocations;

    };
}
