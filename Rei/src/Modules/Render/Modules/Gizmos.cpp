#include "pch.h"
#include "Gizmos.h"

#include "glad/glad.h"
#include "Modules/Behaviour/Components/BehaviourCollection.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "Modules/Render/Material/Material.h"

rei::render::Gizmos::Gizmos(const std::shared_ptr<CameraModule>& cameraModule): _cameraModule(cameraModule)
{
}

void rei::render::Gizmos::Setup()
{
    _gizmosMaterial = GetAssetManager().GetById<Material>(REI_GIZMOS_MATERIAL_ID);
}

void rei::render::Gizmos::RenderBehaviourGizmos() const
{
    ECS_WORLD(GetInternalWorld());

    const auto f = GetInternalWorld().GetFiltersRegistry()->Get<BehaviourCollection>();

    FOR(e, f)
    {
        for (const auto behavioursToUpdate : GET(e, BehaviourCollection).Behaviours)
        {
            GetEntityManager().GetBehaviour(e, behavioursToUpdate).DrawGizmos(*this);
        }
    }
}

void rei::render::Gizmos::RenderBox(const glm::mat4& transformation, const Color& color, bool useDepth) const
{
    RenderBox(transformation, color, useDepth, false);
}

void rei::render::Gizmos::RenderBox(const math::Vector3& pos, const math::Vector3& size, const math::Vector3& rotation, const Color& color,
                                    const bool useDepth) const
{
    RenderBox(GetTransformationMatrix(pos, rotation, size), color, useDepth, false);
}

void rei::render::Gizmos::RenderWireframeBox(const glm::mat4& transformation, const Color& color, bool useDepth) const
{
    RenderBox(transformation, color, useDepth, true);
}

void rei::render::Gizmos::RenderWireframeBox(const math::Vector3& pos, const math::Vector3& size, const math::Vector3& rotation, const Color& color,
                                             const bool useDepth) const
{
    RenderBox(GetTransformationMatrix(pos, rotation, size), color, useDepth, true);
}

void rei::render::Gizmos::RenderBox(const glm::mat4& transformation, const Color& color, bool useDepth, bool wireframe) const
{
    if (wireframe)
    {
        glPolygonMode(GL_FRONT_AND_BACK, GL_LINE);
    }
    if (!useDepth)
    {
        glDisable(GL_DEPTH_TEST);
    }
    else
    {
        glEnable(GL_DEPTH_TEST);
    }

    const auto& shader = _gizmosMaterial.Asset->GetShader();
    shader.SetColor("_Color", color);
    shader.SetViewMatrices(_cameraModule->GetProjectionMatrix(), _cameraModule->GetViewMatrix(), transformation);

    _cubeVertexData.Render();

    glEnable(GL_DEPTH_TEST);
    if (wireframe)
    {
        glPolygonMode(GL_FRONT_AND_BACK, GL_FILL);
    }
}
