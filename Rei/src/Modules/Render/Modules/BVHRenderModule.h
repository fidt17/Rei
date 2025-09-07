#pragma once
#include "GizmosModule.h"

namespace rei::render
{
    class MeshBVHNode;

    class BVHRenderModule
    {
    public:
        explicit BVHRenderModule(const std::shared_ptr<GizmosModule>& gizmosModule);

        void RenderMeshRenderersBVH() const;
        
    private:
        std::shared_ptr<GizmosModule> _gizmosModule;
        void RenderBVH(const MeshBVHNode& node, const glm::mat4& model) const;
    };
}
