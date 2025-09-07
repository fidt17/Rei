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
}

f32 _s = 0.5f;
void rei::render::DefaultRenderScenario::Render()
{
    if (Input::IsKeyDown(GLFW_KEY_DOWN))
    {
        _s -= 0.001f;
    }
    else if (Input::IsKeyDown(GLFW_KEY_UP))
    {
        _s += 0.001f;
    }
    //LOG_WARNING(STRING(_s))
    
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
    RenderSelectionColliders();
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

void rei::render::DefaultRenderScenario::RenderBVH(const MeshBVHNode& node,
                                                   const math::Vector3& pos,
                                                   const math::Vector3& rot,
                                                   const math::Vector3& s) const
{
    
    using math::Vector3;

    auto rootModel = glm::mat4(1.0f);
    rootModel = translate(rootModel, glm::vec3(pos));
    rootModel = rotate(rootModel, glm::radians(rot.x), glm::vec3(1, 0, 0));
    rootModel = rotate(rootModel, glm::radians(rot.y), glm::vec3(0, 1, 0));
    rootModel = rotate(rootModel, glm::radians(rot.z), glm::vec3(0, 0, 1));
    rootModel = scale(rootModel, glm::vec3(s));

    const Vector3 minTransformed = node.Min.Transform(rootModel);
    const Vector3 maxTransformed = node.Max.Transform(rootModel);
    const Vector3 centerTransformed = Vector3::Average(minTransformed, maxTransformed);

    RenderBox(minTransformed, Vector3(0.05f, 0.05f, 0.05f), Vector3(0, 0, 0), Color(0, 1, 0, 1));
    RenderBox(maxTransformed, Vector3(0.05f, 0.05f, 0.05f), Vector3(0, 0, 0), Color(0, 1, 0, 1));
    RenderBox(centerTransformed, Vector3(0.05f, 0.05f, 0.05f), Vector3(0, 0, 0), Color(0, 1, 0, 1));

    auto boxModel = glm::mat4(1.0f);
    boxModel = translate(boxModel, glm::vec3(Vector3::Average(node.Min, node.Max)));
    boxModel = scale(boxModel, glm::vec3((node.Max - node.Min)));
    boxModel = rootModel * boxModel;

    auto rayModel = glm::mat4(1.0f);
    rayModel = translate(rayModel, glm::vec3(Vector3::Average(node.Min, node.Max)));
    rayModel = scale(rayModel, glm::vec3((node.Max - node.Min) * 1.01f));
    rayModel = rootModel * rayModel;
    RenderBox(rayModel, Color(0, 0, 1, 1));

    f32 xPos, yPos;
    Input::GetMousePosition(xPos, yPos);
    const auto ray = Camera::GetMainCamera().Get().GetScreenPointToRay(xPos, yPos);
    //const bool cursorHit = BoxRayIntersection((node.Max - node.Min) / 2, ray, rayModel);
    const bool cursorHit = BoxRayIntersection(Vector3(1,1,1), ray, rayModel);
    //const bool cursorHit = BoxRayIntersection((node.Max - node.Min) / 2, ray, rayModel);

    if (cursorHit)
    {
        LOG_WARNING("------------")
        /*
        LOG_WARNING("Dimensions: " + std::string((node.Max - node.Min)))
        LOG_WARNING("Root Scale: " + std::string(s))
        LOG_WARNING("Scale: " + std::string((node.Max - node.Min) * s))
        LOG_WARNING("Center: " + std::string(Vector3::Average(node.Max, node.Min) * s))
        LOG_WARNING("World Center: " + std::string(centerTransformed))
    */
        
        LOG_WARNING("Dimensions: " + std::string((node.Max - node.Min) / 2))
        LOG_WARNING("Ray origin: " + std::string(ray.Origin))
        LOG_WARNING("Ray direction: " + std::string(ray.Direction))
    }

    RenderBox(boxModel, cursorHit ? Color(0, 1, 0, 1) : Color(1, 1, 1, 1));

    /*
    const auto nodeScale = node.Max - node.Min;
    const auto nodeCenter = Vector3::Average(node.Max, node.Min);

    auto tm = translate(glm::mat4(1.0f), glm::vec3(pos));
    //tm = translate(tm, glm::vec3(nodeCenter * s));
    auto rm = glm::mat4(1.0f);
    rm = rotate(rm, glm::radians(rot.x), glm::vec3(1, 0, 0));
    rm = rotate(rm, glm::radians(rot.y), glm::vec3(0, 1, 0));
    rm = rotate(rm, glm::radians(rot.z), glm::vec3(0, 0, 1));
    auto sm = scale(glm::mat4(1.0f), glm::vec3(s));

    Vector3 dPos = Vector3(0, 0, 0);
    dPos = dPos.Transform(tm);
    Vector3 dScale = nodeScale;
    dScale = dScale.Transform(sm);
    RenderBox(dPos, dScale, Vector3(0, 0, 0), Color(1, 1, 1, 1));
    */

    /*
    auto minTransformed = math::Vector3(glm::vec3(model * glm::vec4(node.Min.x, node.Min.y, node.Min.z, 1)));
    auto maxTransformed = math::Vector3(glm::vec3(model * glm::vec4(node.Max.x, node.Max.y, node.Max.z, 1)));
    const auto boxScale = math::Vector3(node.Max.x - node.Min.x, node.Max.y - node.Min.y, node.Max.z - node.Min.z);
    const auto boxScaleTransformed = math::Vector3(maxTransformed.x - minTransformed.x, maxTransformed.y - minTransformed.y,
                                                   maxTransformed.z - minTransformed.z);
    auto boxModel = scale(model, glm::vec3(boxScale));

    f32 xPos, yPos;
    Input::GetMousePosition(xPos, yPos);
    const auto ray = Camera::GetMainCamera().Get().GetScreenPointToRay(xPos, yPos);

    const bool cursorHit = BoxRayIntersection(node.Min, node.Max, ray, model);

    if (cursorHit)
    {
        LOG_WARNING("Min: " + std::string(minTransformed) + ", Max: " + std::string(maxTransformed))

        RenderBox(minTransformed, math::Vector3(0.25f, 0.25f, 0.25f), math::Vector3(0, 0, 0), Color(1, 1, 1, 1));
        RenderBox(maxTransformed, math::Vector3(0.25f, 0.25f, 0.25f), math::Vector3(0, 0, 0), Color(1, 1, 1, 1));
    }

    math::Vector3 dPos = math::Vector3(0, 0, 0);
    dPos = math::Vector3(glm::vec3(model * glm::vec4(dPos.x, dPos.y, dPos.z, 1)));
    RenderBox(dPos, boxScaleTransformed, math::Vector3(0, 0, 0), Color(1, 1, 1, 1));

    const auto& shader = _lightSourceMaterial.Asset->GetShader();
    shader.SetColor("_Color", cursorHit ? Color(0, 1, 0, 1) : Color(1, 0, 0, 1));
    shader.SetFloat("_Strength", 1);

    shader.SetViewMatrices(_projectionMatrix, _viewMatrix, boxModel);

    glPolygonMode(GL_FRONT_AND_BACK, GL_LINE);
    glDisable(GL_DEPTH_TEST);
    _cubeVertexData.Render();
    glEnable(GL_DEPTH_TEST);
    glPolygonMode(GL_FRONT_AND_BACK, GL_FILL);

    if (node.Left)
    {
        RenderBVH(*node.Left, position, rotation, scale);
    }

    if (node.Right)
    {
        RenderBVH(*node.Right, position, rotation, scale);
    }
*/
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
            RenderBVH(mesh.BVHRoot, transform.GetPosition(), transform.GetRotation(), transform.GetScale());
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

void rei::render::DefaultRenderScenario::RenderSelectionColliders() const
{
    ECS_WORLD(GetInternalWorld());
    const auto f = GetInternalWorld().GetFiltersRegistry()->Get<transformation::Transform, editor::SelectionCollider>();

    FOR(e, f)
    {
        auto& transform = GET(e, transformation::Transform);
        auto& collider = GET(e, editor::SelectionCollider);

        const auto& shader = _lightSourceMaterial.Asset->GetShader();
        shader.SetColor("_Color", Color{0, 1, 0, 1});
        shader.SetFloat("_Strength", 1);

        if (collider.Collider->GetType() == physics::Sphere)
        {
            const std::shared_ptr<physics::SphereCollider>& sphereCollider = std::reinterpret_pointer_cast<physics::SphereCollider>(collider.Collider);

            auto model = glm::mat4(1.0f);
            model = translate(model, glm::vec3(transform.GetPosition()));
            model = rotate(model, glm::radians(transform.GetRotation().x), glm::vec3(1, 0, 0));
            model = rotate(model, glm::radians(transform.GetRotation().y), glm::vec3(0, 1, 0));
            model = rotate(model, glm::radians(transform.GetRotation().z), glm::vec3(0, 0, 1));
            model = scale(model, glm::vec3(sphereCollider->GetRadius()));

            shader.SetViewMatrices(_projectionMatrix, _viewMatrix, model);

            glDisable(GL_DEPTH_TEST);
            _cubeVertexData.Render();
            glEnable(GL_DEPTH_TEST);
        }
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

void rei::render::DefaultRenderScenario::RenderBox(const glm::mat4& transformation, const Color& color) const
{
    glPolygonMode(GL_FRONT_AND_BACK, GL_LINE);
    glDisable(GL_DEPTH_TEST);

    const auto& shader = _lightSourceMaterial.Asset->GetShader();
    shader.SetColor("_Color", color);
    shader.SetFloat("_Strength", 1);

    shader.SetViewMatrices(_projectionMatrix, _viewMatrix, transformation);

    _cubeVertexData.Render();

    glEnable(GL_DEPTH_TEST);
    glPolygonMode(GL_FRONT_AND_BACK, GL_FILL);
}

void rei::render::DefaultRenderScenario::RenderBox(const math::Vector3 pos, const math::Vector3& size, const math::Vector3& rotation, const Color& color) const
{
    auto model = glm::mat4(1.0f);
    model = translate(model, glm::vec3(pos));
    model = rotate(model, glm::radians(rotation.x), glm::vec3(1, 0, 0));
    model = rotate(model, glm::radians(rotation.y), glm::vec3(0, 1, 0));
    model = rotate(model, glm::radians(rotation.z), glm::vec3(0, 0, 1));
    model = scale(model, glm::vec3(size));
    RenderBox(model, color);
}
