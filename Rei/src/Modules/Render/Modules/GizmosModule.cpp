#include "pch.h"
#include "GizmosModule.h"

#include "glad/glad.h"
#include "Modules/Render/Material/Material.h"

rei::render::GizmosModule::GizmosModule(const std::shared_ptr<CameraModule>& cameraModule): _cameraModule(cameraModule)
{
}

void rei::render::GizmosModule::Setup()
{
    _gizmosMaterial = GetAssetManager().GetById<Material>(REI_GIZMOS_MATERIAL_ID);
}

void rei::render::GizmosModule::RenderBox(const glm::mat4& transformation, const Color& color, bool useDepth) const
{
    RenderBox(transformation, color, useDepth, false);
}

void rei::render::GizmosModule::RenderBox(const math::Vector3& pos, const math::Vector3& size, const math::Vector3& rotation, const Color& color, const bool useDepth) const
{
    RenderBox(GetTransformationMatrix(pos, rotation, size), color, useDepth, false);
}

void rei::render::GizmosModule::RenderWireframeBox(const glm::mat4& transformation, const Color& color, bool useDepth) const
{
    RenderBox(transformation, color, useDepth, true);
}

void rei::render::GizmosModule::RenderWireframeBox(const math::Vector3& pos, const math::Vector3& size, const math::Vector3& rotation, const Color& color, const bool useDepth) const
{
    RenderBox(GetTransformationMatrix(pos, rotation, size), color, useDepth, true);
}

void rei::render::GizmosModule::RenderBox(const glm::mat4& transformation, const Color& color, bool useDepth, bool wireframe) const
{
    if (wireframe) glPolygonMode(GL_FRONT_AND_BACK, GL_LINE);
    if (!useDepth) glDisable(GL_DEPTH_TEST);
    else glEnable(GL_DEPTH_TEST);

    const auto& shader = _gizmosMaterial.Asset->GetShader();
    shader.SetColor("_Color", color);
    shader.SetViewMatrices(_cameraModule->GetProjectionMatrix(), _cameraModule->GetViewMatrix(), transformation);

    _cubeVertexData.Render();

    glEnable(GL_DEPTH_TEST);
    if (wireframe) glPolygonMode(GL_FRONT_AND_BACK, GL_FILL);
}
