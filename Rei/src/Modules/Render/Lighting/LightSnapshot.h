#pragma once
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/render/light/AmbientLight.h"
#include "rei_behaviours/render/light/PointLight.h"

namespace rei::render
{
    class LightSnapshot
    {
    public:
        struct PointLightData
        {
            ecs::Entity Entity = ecs::NULL_ENTITY;
            math::Vector3 Position{};
            f32 Strength = 0;
            f32 Range = 0;
            Color LinearColor{0, 0, 0, 1};
        };

        void Update(const ecs::ComponentRef<Camera>& camera);
        f32 GetAmbientStrength() const { return _ambientStrength; }
        const Color& GetAmbientColor() const { return _ambientLinearColor; }
        i32 GetPointLightLimit() const { return _pointLightLimit; }
        u64 GetSelectionRevision() const { return _selectionRevision; }
        const std::vector<PointLightData>& GetPointLights() const { return _pointSnapshot; }

    private:
        void FindAmbientLights();
        void FindPointLights();
        void BuildSnapshot(const ecs::ComponentRef<Camera>& camera);

        ecs::ComponentRef<AmbientLight> _ambientLight{};
        std::vector<ecs::ComponentRef<PointLight>> _pointLights{};
        std::vector<PointLightData> _pointSnapshot{};
        i32 _pointLightLimit = 0;
        f32 _ambientStrength = 0;
        Color _ambientLinearColor{0, 0, 0, 1};
        u64 _selectionRevision = 0;
    };
}
