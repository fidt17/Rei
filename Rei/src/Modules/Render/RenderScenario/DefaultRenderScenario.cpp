#include "pch.h"
#include "DefaultRenderScenario.h"

#include "FrameBuffer.h"
#include "Common/Time/Stopwatch.h"
#include "Engine/Engine.h"
#include "glad/glad.h"
#include <limits>
#include "Modules/Components/ActiveTag.h"
#include "Modules/EntityManagement/EntityManager.h"

#include "Modules/Render/Shaders/Shader.h"
#include "rei_behaviours/render/MeshRenderer.h"
#include "rei_behaviours/render/SpriteRenderer.h"
#include "rei_behaviours/transformation/Transform.h"

rei::render::DefaultRenderScenario::DefaultRenderScenario(GLFWwindow* target)
    : BaseRenderScenario(target),
      _cameraModule(std::make_shared<CameraModule>()),
      _gizmos(std::make_shared<Gizmos>(_cameraModule)),
      _bvh(std::make_shared<BVHRenderModule>(_gizmos)),
      _lighting(std::make_shared<LightingRenderModule>(_cameraModule)),
      _outline(std::make_shared<OutlineRenderModule>(_cameraModule)),
      _postProcessingModule(std::make_shared<PostProcessingModule>(_cameraModule)),
      _gridRenderModule(std::make_shared<GridRenderModule>(_cameraModule, _gizmos)),
      _debugOverlayModule(std::make_shared<DebugOverlayModule>())
{
    Services::GetInstance()->SetGizmos(_gizmos);
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
    _debugOverlayModule->Setup(_target);
}

void rei::render::DefaultRenderScenario::ClearBuffer(const i32 clearMask, const i32 stencilMask) const
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
    time::Stopwatch renderStopwatch;
    renderStopwatch.Start();
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

    renderStopwatch.Stop();
    GetDiagnostics().SetRenderCpuTime(renderStopwatch.ElapsedMs());

    _debugOverlayModule->Render();

    time::Stopwatch presentStopwatch;
    presentStopwatch.Start();
    glfwSwapBuffers(_target);
    presentStopwatch.Stop();
    GetDiagnostics().SetPresentTime(presentStopwatch.ElapsedMs());
}

void rei::render::DefaultRenderScenario::RenderInWireframeMode() const
{
    auto renderMode = _camera.Get().GetRenderMode();
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

    RenderMeshRenderers((std::numeric_limits<i32>::lowest)(), SORTING_ORDER_POST_PROCESSING - 1);
    RenderMeshRenderers(SORTING_ORDER_POST_PROCESSING + 1, SORTING_ORDER_MAX_VALUE);
}

void rei::render::DefaultRenderScenario::RenderInNormalMode()
{
    glPolygonMode(GL_FRONT_AND_BACK, GL_FILL);

    _outline->RenderPass();

    // main pass
    _mainFrameBuffer.EnableBuffer(_cameraModule->GetWidth(), _cameraModule->GetHeight());
    SetBackgroundColor(_cameraModule->GetBackgroundColor());
    ClearBuffer();

    if (GetEngine().IsEditor())
    {
        _gridRenderModule->DrawGrids();
    }

    RenderMeshRenderers((std::numeric_limits<i32>::lowest)(), SORTING_ORDER_POST_PROCESSING - 1);
    _lighting->Render();

    _gizmos->Render();

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

    RenderMeshRenderers(SORTING_ORDER_POST_PROCESSING + 1, SORTING_ORDER_MAX_VALUE);
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
    _debugOverlayModule->Dispose();
}

void rei::render::DefaultRenderScenario::SetBackgroundColor(const Color& color) const
{
    glClearColor(color.r, color.g, color.b, color.a);
}

void rei::render::DefaultRenderScenario::RenderMeshRenderers(const i32 minSortingOrder, const i32 maxSortingOrder) const
{
    ECS_WORLD(rei::GetInternalWorld());
    const auto meshRenderers = FILTER(MeshRenderer, ActiveTag);
    const auto spriteRenderers = FILTER(SpriteRenderer, ActiveTag);

    FOR(e, meshRenderers)
    {
        auto& meshRenderer = GET(e, rei::render::MeshRenderer);
        if (!meshRenderer.IsEnabled()) continue;

        auto& material = meshRenderer.GetRenderMaterial();
        const auto sortingOrder = material.GetSortingOrder();

        if (sortingOrder < minSortingOrder || sortingOrder > maxSortingOrder) continue;

        const Shader& shader = material.GetShader();
        _lighting->SetLightValues(shader);
        shader.SetViewMatrices(_cameraModule->GetProjectionMatrix(), _cameraModule->GetViewMatrix(), meshRenderer.GetTransform().CalculateWorldModelMatrix());
        meshRenderer.Render();
    }

    FOR(e, spriteRenderers)
    {
        auto& spriteRenderer = GET(e, rei::render::SpriteRenderer);
        if (!spriteRenderer.IsEnabled()) continue;

        auto& material = spriteRenderer.GetRenderMaterial();
        const auto sortingOrder = material.GetSortingOrder();

        if (sortingOrder < minSortingOrder || sortingOrder > maxSortingOrder) continue;

        const Shader& shader = material.GetShader();
        _lighting->SetLightValues(shader);
        shader.SetViewMatrices(_cameraModule->GetProjectionMatrix(), _cameraModule->GetViewMatrix(), spriteRenderer.GetTransform().CalculateWorldModelMatrix());
        spriteRenderer.Render();
    }
}

void rei::render::DefaultRenderScenario::RenderMeshRenderersWithOverrideMaterial(const assets::AssetRef<Material>& material) const
{
    ECS_WORLD(rei::GetInternalWorld());
    const auto meshRenderers = FILTER(MeshRenderer, ActiveTag);
    const auto spriteRenderers = FILTER(SpriteRenderer, ActiveTag);

    FOR(e, meshRenderers)
    {
        auto& meshRenderer = GET(e, rei::render::MeshRenderer);
        if (!meshRenderer.IsEnabled()) continue;

        const auto originalMaterial = meshRenderer.GetMaterial();
        meshRenderer.SetMaterial(material);

        const Shader& shader = meshRenderer.GetRenderMaterial().GetShader();
        _lighting->SetLightValues(shader);
        shader.SetViewMatrices(_cameraModule->GetProjectionMatrix(), _cameraModule->GetViewMatrix(), meshRenderer.GetTransform().CalculateWorldModelMatrix());
        meshRenderer.Render();

        meshRenderer.SetMaterial(originalMaterial);
    }

    FOR(e, spriteRenderers)
    {
        auto& spriteRenderer = GET(e, rei::render::SpriteRenderer);
        if (!spriteRenderer.IsEnabled()) continue;
        if (!spriteRenderer.GetModel().IsLoaded()) continue;

        const Shader& shader = material->GetShader();
        _lighting->SetLightValues(shader);
        shader.SetViewMatrices(_cameraModule->GetProjectionMatrix(), _cameraModule->GetViewMatrix(), spriteRenderer.GetTransform().CalculateWorldModelMatrix());
        material->Use();

        for (const auto& mesh : spriteRenderer.GetModel()->GetMeshes())
        {
            mesh.Render();
        }
    }
}
