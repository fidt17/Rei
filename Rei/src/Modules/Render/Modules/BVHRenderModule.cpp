#include "pch.h"
#include "BVHRenderModule.h"

#include "Modules/Render/Color/Color.h"
#include "Modules/Render/Mesh/MeshBVHNode.h"
#include "rei_behaviours/render/MeshRenderer.h"
#include "rei_behaviours/transformation/Transform.h"

rei::render::BVHRenderModule::BVHRenderModule(const std::shared_ptr<GizmosModule>& gizmosModule): _gizmosModule(gizmosModule)
{
}

void rei::render::BVHRenderModule::RenderBVH(const MeshBVHNode& node, const glm::mat4& model) const
{
    using math::Vector3;

    if (node.Left)
    {
        RenderBVH(*node.Left, model);
    }

    if (node.Right)
    {
        RenderBVH(*node.Right, model);
    }

    if (!node.Left && !node.Right)
    {
        auto boxModel = glm::mat4(1.0f);
        boxModel = translate(boxModel, glm::vec3(Vector3::Average(node.Min, node.Max)));
        boxModel = scale(boxModel, glm::vec3((node.Max - node.Min)));
        boxModel = model * boxModel;

        _gizmosModule->RenderWireframeBox(boxModel, Color::White());
    }
}

void rei::render::BVHRenderModule::RenderMeshRenderersBVH() const
{
    ECS_WORLD(rei::GetInternalWorld());
    const auto f = GetInternalWorld().GetFiltersRegistry()->Get<MeshRenderer>();

    FOR(e, f)
    {
        auto& meshRenderer = GET(e, rei::render::MeshRenderer);
        if (!meshRenderer.GetModel().IsLoaded) continue;

        for (const auto& mesh : meshRenderer.GetModel().Asset->GetMeshes())
        {
            auto& transform = meshRenderer.GetTransform();
            RenderBVH(mesh.BVHRoot, transform.CalculateModelMatrix());
        }
    }
}
