#pragma once
#include "../../../../resources/meshes/CubeVertexData.h"

namespace rei::render
{
    class Material;
    struct Color;

    class GizmosModule
    {
    public:
        void Setup();
        void OnBeforeRender(const glm::mat4& projectionMatrix, const glm::mat4& viewMatrix);
        
        void RenderBox(const glm::mat4& transformation, const Color& color, bool useDepth = true) const;
        void RenderBox(const math::Vector3& pos, const math::Vector3& size, const math::Vector3& rotation, const Color& color, bool useDepth = true) const;
        
        void RenderWireframeBox(const glm::mat4& transformation, const Color& color, bool useDepth = true) const;
        void RenderWireframeBox(const math::Vector3& pos, const math::Vector3& size, const math::Vector3& rotation, const Color& color, bool useDepth = true) const;

    private:
        void RenderBox(const glm::mat4& transformation, const Color& color, bool useDepth, bool wireframe) const;

    private:
        glm::mat4 _projectionMatrix = 0;
        glm::mat4 _viewMatrix = 0;
        
        assets::AssetRef<Material> _gizmosMaterial{};
        
        CubeVertexData _cubeVertexData;
    };
}
