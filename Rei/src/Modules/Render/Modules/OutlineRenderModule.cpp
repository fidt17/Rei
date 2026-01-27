#include "pch.h"
#include "OutlineRenderModule.h"

#include "rei_behaviours/render/MeshRenderer.h"
#include "rei_behaviours/render/RenderOutlineTag.h"
#include "rei_behaviours/transformation/Transform.h"

rei::render::OutlineRenderModule::OutlineRenderModule(const std::shared_ptr<CameraModule>& cameraModule): _cameraModule(cameraModule)
{
}

void rei::render::OutlineRenderModule::Setup()
{
    _outlineQuadMaterial = GetAssetManager().GetById<Material>(REI_OUTLINE_MATERIAL_ID);
}

void rei::render::OutlineRenderModule::RenderPass()
{
    _outlineObjectsBuffer.EnableBuffer(_cameraModule->GetWidth(), _cameraModule->GetHeight());
    glClearColor(0,0,0,0);
    glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT | GL_STENCIL_BUFFER_BIT);

    RenderOutlineObjects();
}

void rei::render::OutlineRenderModule::RenderOutlineFrame() const
{
    _outlineQuadMaterial.Asset->GetShader().Use();

    glDisable(GL_DEPTH_TEST);

    glActiveTexture(GL_TEXTURE0 + 0);
    glBindTexture(GL_TEXTURE_2D, _outlineObjectsBuffer.GetColorTexture());
    _quadVertexData.Render();

    glEnable(GL_DEPTH_TEST);
}

void rei::render::OutlineRenderModule::RenderOutlineObjects() const
{
    ECS_WORLD(rei::GetInternalWorld());
    const auto meshRenderers = FILTER(MeshRenderer, RenderOutlineTag);

    FOR(e, meshRenderers)
    {
        const auto& meshRenderer = GET(e, rei::render::MeshRenderer);

        const Shader& shader = meshRenderer.GetRenderMaterial().GetShader();
        shader.SetViewMatrices(_cameraModule->GetProjectionMatrix(), _cameraModule->GetViewMatrix(), meshRenderer.GetTransform().CalculateModelMatrix());
        meshRenderer.Render();
    }
}

