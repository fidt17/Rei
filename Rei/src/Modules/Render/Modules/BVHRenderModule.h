#pragma once
#include "Gizmos.h"

namespace rei::render
{
    class MeshBVHNode;

    class BVHRenderModule
    {
    public:
        explicit BVHRenderModule(const std::shared_ptr<Gizmos>& gizmosModule);

        void Render() const;
        
    private:
        std::shared_ptr<Gizmos> _gizmosModule;
        void RenderBVH(const MeshBVHNode& node, const glm::mat4& model) const;
    };
}
