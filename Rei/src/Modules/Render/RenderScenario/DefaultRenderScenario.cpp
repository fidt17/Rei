#include "pch.h"
#include "DefaultRenderScenario.h"

#include "FrameBuffer.h"
#include "../../../../resources/rei_behaviours/render/MeshRenderer.h"
#include "../../../../resources/rei_behaviours/render/RenderOutlineTag.h"
#include "../../../../resources/rei_behaviours/render/light/AmbientLight.h"
#include "../../../../resources/rei_behaviours/render/light/PointLight.h"
#include "../../../../resources/rei_behaviours/transformation/Transform.h"
#include "glad/glad.h"
#include "Modules/Editor/SelectionCollider.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "Modules/Input/Input.h"
#include "Modules/Physics/SphereCollider.h"

#include "Modules/Render/Shaders/Shader.h"

rei::render::DefaultRenderScenario::DefaultRenderScenario(GLFWwindow* target)
    : BaseRenderScenario(target)
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
    _lightSourceMaterial = GetAssetManager().GetById<Material>(REI_LIGHT_SOURCE_MATERIAL_ID);
    _depthMaterial = GetAssetManager().GetById<Material>(REI_DEPTH_MATERIAL_ID);

    _gizmosModule.Setup();
}

void rei::render::DefaultRenderScenario::ClearBuffer(const int clearMask, const i32 stencilMask) const
{
    glStencilMask(stencilMask);
    glClear(clearMask);
    glStencilMask(0x00);
}

void rei::render::DefaultRenderScenario::OnBeforeRender()
{
    FindAmbientLights();
    FindPointLights();

    _projectionMatrix = _camera.Get().GetProjectionMatrix();
    _viewMatrix = _camera.Get().GetViewMatrix();
    _camera.Get().GetOutputSize(_outputWidth, _outputHeight);

    _gizmosModule.OnBeforeRender(_projectionMatrix, _viewMatrix);
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

    RenderPointLights();
    RenderMeshRenderersBVH();
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

void rei::render::DefaultRenderScenario::FindAmbientLights()
{
    ECS_WORLD(rei::GetInternalWorld());
    const auto f = GetInternalWorld().GetFiltersRegistry()->Get<AmbientLight>();
    GetInternalWorld().RefreshAll();

    if (f->GetEntitiesCount() == 0) return;
    _ambientLight = GET_REF(*f->begin(), rei::render::AmbientLight);
}

void rei::render::DefaultRenderScenario::FindPointLights()
{
    ECS_WORLD(rei::GetInternalWorld());
    const auto f = GetInternalWorld().GetFiltersRegistry()->Get<PointLight>();
    GetInternalWorld().RefreshAll();

    _pointLights.clear();
    FOR(e, f)
    {
        _pointLights.emplace_back(GET_REF(e, rei::render::PointLight));
    }
}

void rei::render::DefaultRenderScenario::SetAmbientLight(const Shader& shader) const
{
    if (_ambientLight.IsNull())
    {
        shader.SetFloat("_AmbientLight.Strength", 0);
        shader.SetColor("_AmbientLight.Color", Color(0, 0, 0, 1));
        return;
    }

    shader.SetFloat("_AmbientLight.Strength", _ambientLight.Get().GetStrength());

    const auto& c = _ambientLight.Get().GetColor();
    shader.SetColor("_AmbientLight.Color", c);
}

void rei::render::DefaultRenderScenario::SetPointLights(const Shader& shader) const
{
    for (int i = 0; i < _pointLights.size(); i++)
    {
        if (i > REI_MAX_POINT_LIGHTS_COUNT) break;

        const auto& light = _pointLights[i];
        if (light.IsNull()) continue;

        shader.SetVector3("_PointLights[" + std::to_string(i) + "].Position", light.Get().GetTransform().GetPosition());
        shader.SetFloat("_PointLights[" + std::to_string(i) + "].Strength", light.Get().GetStrength());
        shader.SetColor("_PointLights[" + std::to_string(i) + "].Color", light.Get().GetColor());
    }
}

void rei::render::DefaultRenderScenario::RenderMeshRenderers() const
{
    ECS_WORLD(rei::GetInternalWorld());
    const auto f = GetInternalWorld().GetFiltersRegistry()->Get<MeshRenderer>();

    FOR(e, f)
    {
        const auto& meshRenderer = GET(e, rei::render::MeshRenderer);

        const Shader& shader = meshRenderer.GetRenderShader();
        SetAmbientLight(shader);
        SetPointLights(shader);
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
        SetAmbientLight(shader);
        SetPointLights(shader);
        shader.SetViewMatrices(_projectionMatrix, _viewMatrix, meshRenderer.GetTransform().CalculateModelMatrix());
        meshRenderer.Render();

        meshRenderer.SetMaterial(originalMaterial);
    }
}

void rei::render::DefaultRenderScenario::RenderBVH(const MeshBVHNode& node, const glm::mat4& model) const
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

        _gizmosModule.RenderWireframeBox(boxModel, Color::White());
    }
}

void rei::render::DefaultRenderScenario::RenderMeshRenderersBVH() const
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

void rei::render::DefaultRenderScenario::RenderPointLights() const
{
    for (auto& light : _pointLights)
    {
        if (light.IsNull()) return;

        const auto& shader = _lightSourceMaterial.Asset->GetShader();
        shader.SetColor("_Color", light.Get().GetColor());
        shader.SetFloat("_Strength", light.Get().GetStrength());
        shader.SetViewMatrices(_projectionMatrix, _viewMatrix, light.Get().GetTransform().CalculateModelMatrix());

        _cubeVertexData.Render();
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

