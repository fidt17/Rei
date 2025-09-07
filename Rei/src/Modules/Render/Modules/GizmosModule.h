#pragma once
#include "../../../../resources/meshes/CubeVertexData.h"
#include "Modules/Render/RenderScenario/CameraModule.h"

namespace rei::render
{
    class Material;
    struct Color;

    class GizmosModule
    {
    public:
        explicit GizmosModule(const std::shared_ptr<CameraModule>& cameraModule);

        void Setup();
        
        void RenderBox(const glm::mat4& transformation, const Color& color, bool useDepth = true) const;
        void RenderBox(const math::Vector3& pos, const math::Vector3& size, const math::Vector3& rotation, const Color& color, bool useDepth = true) const;
        
        void RenderWireframeBox(const glm::mat4& transformation, const Color& color, bool useDepth = true) const;
        void RenderWireframeBox(const math::Vector3& pos, const math::Vector3& size, const math::Vector3& rotation, const Color& color, bool useDepth = true) const;

    private:
        void RenderBox(const glm::mat4& transformation, const Color& color, bool useDepth, bool wireframe) const;

    private:
        std::shared_ptr<CameraModule> _cameraModule;
        
        assets::AssetRef<Material> _gizmosMaterial{};
        
        CubeVertexData _cubeVertexData;
    };
}
