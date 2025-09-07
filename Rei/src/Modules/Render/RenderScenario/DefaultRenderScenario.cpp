#include "pch.h"
#include "DefaultRenderScenario.h"

#include "FrameBuffer.h"
#include "glad/glad.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "Modules/Input/Input.h"
#include "Modules/Physics/SphereCollider.h"

#include "Modules/Render/Shaders/Shader.h"
#include "rei_behaviours/render/MeshRenderer.h"
#include "rei_behaviours/render/RenderOutlineTag.h"
#include "rei_behaviours/transformation/Transform.h"

rei::render::DefaultRenderScenario::DefaultRenderScenario(GLFWwindow* target)
    : BaseRenderScenario(target),
      _gizmos(std::make_shared<GizmosModule>()),
      _bvh(std::make_shared<BVHRenderModule>(_gizmos)),
      _lighting(std::make_shared<LightingRenderModule>())
{
}

void rei::render::DefaultRenderScenario::Setup()
{
    glEnable(GL_DEPTH_TEST);

    glEnable(GL_STENCIL_TEST);
    glStencilOp(GL_KEEP, GL_REPLACE, GL_REPLACE);

    glEnable(GL_BLEND);
    glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
    glEnable(GL_MULTISAMPLE);

    _overlayMaterial = GetAssetManager().GetById<Material>(REI_OVERLAY_TEXTURE_MATERIAL_ID);
    _grayscaleMaterial = GetAssetManager().GetById<Material>(REI_OVERLAY_GRAYSCALE_MATERIAL_ID);
    _inversionMaterial = GetAssetManager().GetById<Material>(REI_OVERLAY_INVERSION_MATERIAL_ID);

    _outlineQuadMaterial = GetAssetManager().GetById<Material>(REI_OUTLINE_MATERIAL_ID);
    _depthMaterial = GetAssetManager().GetById<Material>(REI_DEPTH_MATERIAL_ID);

    _gizmos->Setup();
    _lighting->Setup();
}

void rei::render::DefaultRenderScenario::ClearBuffer(const int clearMask, const i32 stencilMask) const
{
    glStencilMask(stencilMask);
    glClear(clearMask);
    glStencilMask(0x00);
}

void rei::render::DefaultRenderScenario::OnBeforeRender()
{
    _projectionMatrix = _camera.Get().GetProjectionMatrix();
    _viewMatrix = _camera.Get().GetViewMatrix();
    _camera.Get().GetOutputSize(_outputWidth, _outputHeight);

    _gizmos->OnBeforeRender(_projectionMatrix, _viewMatrix);
    _lighting->OnBeforeRender(_projectionMatrix, _viewMatrix);
}

void rei::render::DefaultRenderScenario::Render()
{
    const auto renderMode = _camera.Get().GetRenderMode();
    if (renderMode == WireframeLines || renderMode == WireframePoints)
    {
        RenderInWireframeMode();
    }
    else if (renderMode == Depth)
    {
        RenderInDepthMode();
    }
    else
    {
        RenderInNormalMode();
    }

    glfwSwapBuffers(_target);
}

void rei::render::DefaultRenderScenario::RenderInWireframeMode() const
{
    const auto renderMode = _camera.Get().GetRenderMode();
    if (renderMode == WireframeLines)
    {
        glPolygonMode(GL_FRONT_AND_BACK, GL_LINE);
    }
    else if (renderMode == WireframePoints)
    {
        glPolygonMode(GL_FRONT_AND_BACK, GL_POINT);
    }

    glBindFramebuffer(GL_FRAMEBUFFER, 0);
    SetBackgroundColor(_camera.Get().GetBackgroundColor());
    ClearBuffer();

    RenderMeshRenderers();
}

void rei::render::DefaultRenderScenario::RenderInNormalMode()
{
    glPolygonMode(GL_FRONT_AND_BACK, GL_FILL);

    // selected objects pass
    _outlineObjectsBuffer.EnableBuffer(_outputWidth, _outputHeight);
    SetBackgroundColor(Color(0, 0, 0, 0));
    ClearBuffer();

    RenderOutlineObjects();
    // ------

    // main pass
    _mainFrameBuffer.EnableBuffer(_outputWidth, _outputHeight);
    SetBackgroundColor(_camera.Get().GetBackgroundColor());
    ClearBuffer();

    RenderMeshRenderers();

    _lighting->Render();
    _bvh->Render();
    // ------

    // post processing
    glBindFramebuffer(GL_FRAMEBUFFER, 0);
    ClearBuffer();
    RenderPostprocessing();
    RenderOutlineFrame();
    // ------
}

void rei::render::DefaultRenderScenario::RenderInDepthMode()
{
    glPolygonMode(GL_FRONT_AND_BACK, GL_FILL);

    // selected objects pass
    _outlineObjectsBuffer.EnableBuffer(_outputWidth, _outputHeight);
    SetBackgroundColor(Color(0, 0, 0, 0));
    ClearBuffer();

    RenderOutlineObjects();
    // ------

    // main pass
    glBindFramebuffer(GL_FRAMEBUFFER, 0);
    SetBackgroundColor(Color(0, 0, 0, 0));
    ClearBuffer();

    RenderMeshRenderersWithOverrideMaterial(_depthMaterial);
    RenderOutlineFrame();
    // ------
}

void rei::render::DefaultRenderScenario::Dispose()
{
}

void rei::render::DefaultRenderScenario::SetBackgroundColor(const Color& color) const
{
    glClearColor(color.r, color.g, color.b, color.a);
}

void rei::render::DefaultRenderScenario::RenderMeshRenderers() const
{
    ECS_WORLD(rei::GetInternalWorld());
    const auto f = GetInternalWorld().GetFiltersRegistry()->Get<MeshRenderer>();

    FOR(e, f)
    {
        const auto& meshRenderer = GET(e, rei::render::MeshRenderer);

        const Shader& shader = meshRenderer.GetRenderShader();
        _lighting->SetLightValues(shader);
        shader.SetViewMatrices(_projectionMatrix, _viewMatrix, meshRenderer.GetTransform().CalculateModelMatrix());
        meshRenderer.Render();
    }
}

void rei::render::DefaultRenderScenario::RenderMeshRenderersWithOverrideMaterial(const assets::AssetRef<Material>& material) const
{
    ECS_WORLD(rei::GetInternalWorld());
    const auto f = GetInternalWorld().GetFiltersRegistry()->Get<MeshRenderer>();

    FOR(e, f)
    {
        auto& meshRenderer = GET(e, rei::render::MeshRenderer);

        const auto originalMaterial = meshRenderer.GetMaterial();
        meshRenderer.SetMaterial(material);

        const Shader& shader = meshRenderer.GetRenderShader();
        _lighting->SetLightValues(shader);
        shader.SetViewMatrices(_projectionMatrix, _viewMatrix, meshRenderer.GetTransform().CalculateModelMatrix());
        meshRenderer.Render();

        meshRenderer.SetMaterial(originalMaterial);
    }
}


void rei::render::DefaultRenderScenario::RenderOutlineObjects() const
{
    ECS_WORLD(rei::GetInternalWorld());
    const auto f = GetInternalWorld().GetFiltersRegistry()->Get<MeshRenderer, RenderOutlineTag>();

    FOR(e, f)
    {
        const auto& meshRenderer = GET(e, rei::render::MeshRenderer);

        const Shader& shader = meshRenderer.GetRenderShader();
        shader.SetViewMatrices(_projectionMatrix, _viewMatrix, meshRenderer.GetTransform().CalculateModelMatrix());
        meshRenderer.Render();
    }
}

void rei::render::DefaultRenderScenario::RenderOutlineFrame() const
{
    _outlineQuadMaterial.Asset->GetShader().Use();

    glDisable(GL_DEPTH_TEST);

    glActiveTexture(GL_TEXTURE0 + 0);
    glBindTexture(GL_TEXTURE_2D, _outlineObjectsBuffer.GetColorTexture());
    _quadVertexData.Render();

    glEnable(GL_DEPTH_TEST);
}

void rei::render::DefaultRenderScenario::RenderPostprocessing() const
{
    const auto renderMode = _camera.Get().GetRenderMode();

    auto material = _overlayMaterial;

    if (renderMode == Grayscale)
    {
        material = _grayscaleMaterial;
    }
    else if (renderMode == Inversion)
    {
        material = _inversionMaterial;
    }

    material.Asset->GetShader().Use();

    glActiveTexture(GL_TEXTURE0 + 0);
    glBindTexture(GL_TEXTURE_2D, _mainFrameBuffer.GetColorTexture());
    _quadVertexData.Render();
}
