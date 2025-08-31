#include "pch.h"
#include "DefaultRenderScenario.h"

#include "../../../../resources/rei_behaviours/render/MeshRenderer.h"
#include "../../../../resources/rei_behaviours/render/RenderOutlineTag.h"
#include "../../../../resources/rei_behaviours/render/light/AmbientLight.h"
#include "../../../../resources/rei_behaviours/render/light/PointLight.h"
#include "../../../../resources/rei_behaviours/transformation/Transform.h"
#include "glad/glad.h"

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
}

void rei::render::DefaultRenderScenario::ResetBuffers() const
{
    glStencilMask(0xFF);
    glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT | GL_STENCIL_BUFFER_BIT);
    glStencilMask(0x00);
}

void rei::render::DefaultRenderScenario::Render()
{
    FindAmbientLights();
    FindPointLights();

    _projectionMatrix = _camera.Get().GetProjectionMatrix();
    _viewMatrix = _camera.Get().GetViewMatrix();

    SetPolygonMode();
    SetBackgroundColor();

    ResetBuffers();

    RenderMeshRenderers();
    RenderPointLights();

    RenderOutlines();

    glfwSwapBuffers(_target);
}

void rei::render::DefaultRenderScenario::SetPolygonMode() const
{
    const auto renderMode = _camera.Get().GetRenderMode();
    if (renderMode == Shaded)
    {
        glPolygonMode(GL_FRONT_AND_BACK, GL_FILL);
    }
    else if (renderMode == WireframeLines)
    {
        glPolygonMode(GL_FRONT_AND_BACK, GL_LINE);
    }
    else if (renderMode == WireframePoints)
    {
        glPolygonMode(GL_FRONT_AND_BACK, GL_POINT);
    }
}

void rei::render::DefaultRenderScenario::Dispose()
{
}

void rei::render::DefaultRenderScenario::SetBackgroundColor() const
{
    const auto& color = _camera.Get().GetBackgroundColor();
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

        // render mesh normally
        const Shader& shader = meshRenderer.GetRenderShader();
        SetAmbientLight(shader);
        SetPointLights(shader);
        shader.SetViewMatrices(_projectionMatrix, _viewMatrix, meshRenderer.GetTransform().CalculateModelMatrix());
        meshRenderer.Render();

        // render to stencil buffer with disabled depth test
        if (HAS(e, RenderOutlineTag))
        {
            glStencilFunc(GL_ALWAYS, 1, 0xFF); // mark fragments as 1 with mask of 0xFF
            glStencilMask(0xFF); // set target mask

            glDisable(GL_DEPTH_TEST);
            glColorMask(GL_FALSE, GL_FALSE, GL_FALSE, GL_FALSE);

            meshRenderer.Render();

            glEnable(GL_DEPTH_TEST);
            glColorMask(GL_TRUE, GL_TRUE, GL_TRUE, GL_TRUE);

            glStencilMask(0x00); // reset stencil mask
        }
    }
}

void rei::render::DefaultRenderScenario::RenderOutlines() const
{
    ECS_WORLD(rei::GetInternalWorld());
    const auto f = GetInternalWorld().GetFiltersRegistry()->Get<MeshRenderer, RenderOutlineTag>();

    glDisable(GL_DEPTH_TEST);
    glStencilFunc(GL_NOTEQUAL, 1, 0xFF); // draw everywhere except for fragments marked as 1 on mask of 0xFF
    glStencilMask(0x00); // do not override stencil data
    
    FOR(e, f)
    {
        // render mesh with outline shader
        const auto& meshRenderer = GET(e, rei::render::MeshRenderer);
        const Shader& outlineShader = meshRenderer.GetOutlineShader();
        outlineShader.SetViewMatrices(_projectionMatrix, _viewMatrix, meshRenderer.GetTransform().CalculateModelMatrix());
        meshRenderer.RenderOutline();
    }

    glEnable(GL_DEPTH_TEST);
    glStencilFunc(GL_ALWAYS, 1, 0x00);
}

void rei::render::DefaultRenderScenario::RenderPointLights() const
{
    for (auto& light : _pointLights)
    {
        if (light.IsNull()) return;

        const auto& material = GetAssetManager().GetById<Material>(REI_LIGHT_SOURCE_MATERIAL_ID);
        material.Asset->GetShader().SetColor("_Color", light.Get().GetColor());
        material.Asset->GetShader().SetFloat("_Strength", light.Get().GetStrength());
        material.Asset->GetShader().SetViewMatrices(_projectionMatrix, _viewMatrix, light.Get().GetTransform().CalculateModelMatrix());

        _cubeVertexData.Render();
    }
}
