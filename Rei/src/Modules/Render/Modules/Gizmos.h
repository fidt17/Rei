#pragma once
#include "../../../../resources/meshes/CubeVertexData.h"
#include "Modules/Render/RenderScenario/CameraModule.h"

namespace rei::render
{
    class Material;
    struct Color;

    class Gizmos
    {
    public:
        explicit Gizmos(const std::shared_ptr<CameraModule>& cameraModule);

        void Setup();

        void RenderBehaviourGizmos() const;
        
        REI_API void RenderBox(const glm::mat4& transformation, const Color& color, bool useDepth = true) const;
        REI_API void RenderBox(const math::Vector3& pos, const math::Vector3& size, const math::Vector3& rotation, const Color& color, bool useDepth = true) const;
        
        REI_API void RenderWireframeBox(const glm::mat4& transformation, const Color& color, bool useDepth = true) const;
        REI_API void RenderWireframeBox(const math::Vector3& pos, const math::Vector3& size, const math::Vector3& rotation, const Color& color, bool useDepth = true) const;

    private:
        void RenderBox(const glm::mat4& transformation, const Color& color, bool useDepth, bool wireframe) const;

    private:
        std::shared_ptr<CameraModule> _cameraModule;
        
        assets::AssetRef<Material> _gizmosMaterial{};
        
        CubeVertexData _cubeVertexData;
    };
}
