#include "pch.h"
#include "DefaultRenderScenario.h"

#include "FrameBuffer.h"
#include "glad/glad.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "Modules/Input/Input.h"
#include "Modules/Physics/SphereCollider.h"

#include "Modules/Render/Shaders/Shader.h"
#include "rei_behaviours/render/MeshRenderer.h"
#include "rei_behaviours/transformation/Transform.h"

rei::render::DefaultRenderScenario::DefaultRenderScenario(GLFWwindow* target)
    : BaseRenderScenario(target),
      _cameraModule(std::make_shared<CameraModule>()),
      _gizmos(std::make_shared<Gizmos>(_cameraModule)),
      _bvh(std::make_shared<BVHRenderModule>(_gizmos)),
      _lighting(std::make_shared<LightingRenderModule>(_cameraModule)),
      _outline(std::make_shared<OutlineRenderModule>(_cameraModule)),
      _postProcessingModule(std::make_shared<PostProcessingModule>(_cameraModule)),
      _gridRenderModule(std::make_shared<GridRenderModule>(_cameraModule, _gizmos))
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

    _depthMaterial = GetAssetManager().GetById<Material>(REI_DEPTH_MATERIAL_ID);

    _gizmos->Setup();
    _lighting->Setup();
    _outline->Setup();
    _postProcessingModule->Setup();
    _gridRenderModule->Setup();
}

void rei::render::DefaultRenderScenario::ClearBuffer(const int clearMask, const i32 stencilMask) const
{
    glStencilMask(stencilMask);
    glClear(clearMask);
    glStencilMask(0x00);
}

void rei::render::DefaultRenderScenario::OnBeforeRender()
{
    _cameraModule->OnBeforeRender();
    _lighting->OnBeforeRender();
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

    _outline->RenderPass();

    // main pass
    _mainFrameBuffer.EnableBuffer(_cameraModule->GetWidth(), _cameraModule->GetHeight());
    SetBackgroundColor(_cameraModule->GetBackgroundColor());
    ClearBuffer();

    _gizmos->RenderBehaviourGizmos();
    _gridRenderModule->DrawGrids();
    
    RenderMeshRenderers();

    _lighting->Render();

    if (_cameraModule->GetCamera().Get().GetRenderMode() == BVH)
    {
        _bvh->Render();
    }
    // ------

    // post processing
    glBindFramebuffer(GL_FRAMEBUFFER, 0);
    ClearBuffer();
    _postProcessingModule->Render(_mainFrameBuffer);
    _outline->RenderOutlineFrame();
    // ------
}

void rei::render::DefaultRenderScenario::RenderInDepthMode() const
{
    glPolygonMode(GL_FRONT_AND_BACK, GL_FILL);

    _outline->RenderPass();

    // main pass
    glBindFramebuffer(GL_FRAMEBUFFER, 0);
    SetBackgroundColor(Color(0, 0, 0, 0));
    ClearBuffer();

    RenderMeshRenderersWithOverrideMaterial(_depthMaterial);
    _outline->RenderOutlineFrame();
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
        shader.SetViewMatrices(_cameraModule->GetProjectionMatrix(), _cameraModule->GetViewMatrix(), meshRenderer.GetTransform().CalculateModelMatrix());
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
        shader.SetViewMatrices(_cameraModule->GetProjectionMatrix(), _cameraModule->GetViewMatrix(), meshRenderer.GetTransform().CalculateModelMatrix());
        meshRenderer.Render();

        meshRenderer.SetMaterial(originalMaterial);
    }
}
