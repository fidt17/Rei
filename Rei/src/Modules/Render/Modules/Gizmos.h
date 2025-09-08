#pragma once
#include "../../../../resources/meshes/CubeVertexData.h"
#include "meshes/LineVertexData.h"
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

        REI_API void DrawLine(const math::Vector3& start, const math::Vector3& end, const Color& color, bool useDepth = true) const;
        
        REI_API void DrawBox(const glm::mat4& transformation, const Color& color, bool useDepth = true) const;
        REI_API void DrawBox(const math::Vector3& pos, const math::Vector3& size, const math::Vector3& rotation, const Color& color, bool useDepth = true) const;
        
        REI_API void DrawWireframeBox(const glm::mat4& transformation, const Color& color, bool useDepth = true) const;
        REI_API void DrawWireframeBox(const math::Vector3& pos, const math::Vector3& size, const math::Vector3& rotation, const Color& color, bool useDepth = true) const;

    private:
        void DrawBox(const glm::mat4& transformation, const Color& color, bool useDepth, bool wireframe) const;

    private:
        std::shared_ptr<CameraModule> _cameraModule;
        
        assets::AssetRef<Material> _gizmosMaterial{};
        
        CubeVertexData _cubeMesh;
        LineVertexData _lineMesh;
    };
}
