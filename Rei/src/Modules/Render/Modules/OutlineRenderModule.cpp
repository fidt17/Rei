#include "pch.h"
#include "OutlineRenderModule.h"

#include "Common/Transform/RectTransformUtility.h"
#include "glm/ext/matrix_clip_space.hpp"
#include "Modules/Components/ActiveTag.h"
#include "rei_behaviours/render/MeshRenderer.h"
#include "rei_behaviours/render/RenderOutlineTag.h"
#include "rei_behaviours/render/SpriteRenderer.h"
#include "rei_behaviours/transformation/Transform.h"
#include "rei_behaviours/ui/Canvas.h"
#include "rei_behaviours/ui/Image.h"
#include "rei_behaviours/ui/RectTransform.h"

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
    _outlineQuadMaterial->GetShader().Use();

    glDisable(GL_DEPTH_TEST);

    glActiveTexture(GL_TEXTURE0 + 0);
    glBindTexture(GL_TEXTURE_2D, _outlineObjectsBuffer.GetColorTexture());
    _quadVertexData.Render();

    glEnable(GL_DEPTH_TEST);
}

void rei::render::OutlineRenderModule::RenderOutlineObjects() const
{
    RenderMeshOutlines();
    RenderSpriteOutlines();
    RenderUiImageOutlines();
}

void rei::render::OutlineRenderModule::RenderMeshOutlines() const
{
    ECS_WORLD(rei::GetInternalWorld());
    const auto meshRenderers = FILTER(MeshRenderer, RenderOutlineTag);

    FOR(e, meshRenderers)
    {
        const auto& meshRenderer = GET(e, rei::render::MeshRenderer);

        const Shader& shader = meshRenderer.GetRenderMaterial().GetShader();
        shader.SetViewMatrices(_cameraModule->GetProjectionMatrix(), _cameraModule->GetViewMatrix(), meshRenderer.GetTransform().CalculateWorldModelMatrix());
        meshRenderer.Render();
    }
}

void rei::render::OutlineRenderModule::RenderSpriteOutlines() const
{
    ECS_WORLD(rei::GetInternalWorld());
    const auto spriteRenderers = FILTER(SpriteRenderer, RenderOutlineTag);

    FOR(e, spriteRenderers)
    {
        const auto& spriteRenderer = GET(e, rei::render::SpriteRenderer);

        const Shader& shader = spriteRenderer.GetRenderMaterial().GetShader();
        shader.SetViewMatrices(_cameraModule->GetProjectionMatrix(), _cameraModule->GetViewMatrix(), spriteRenderer.GetTransform().CalculateWorldModelMatrix());
        spriteRenderer.Render();
    }
}

void rei::render::OutlineRenderModule::RenderUiImageOutlines() const
{
    ECS_WORLD(rei::GetInternalWorld());
    const auto uiImages = FILTER(ui::Image, ui::RectTransform, Transform, RenderOutlineTag, ActiveTag);

    const glm::mat4 projection = glm::ortho(0.0f, static_cast<f32>(_cameraModule->GetWidth()), 0.0f, static_cast<f32>(_cameraModule->GetHeight()), -1.0f, 1.0f);
    const glm::mat4 view = glm::mat4(1.0f);
    FOR(e, uiImages)
    {
        const auto& image = GET(e, rei::ui::Image);
        if (!image.IsEnabled()) continue;

        const auto canvasEntity = ui_utility::FindCanvasEntity(e);
        if (IS_DEAD(canvasEntity) || !HAS(canvasEntity, rei::ui::Canvas)) continue;

        const auto& canvas = GET(canvasEntity, rei::ui::Canvas);
        const auto logicalRect = ui_utility::CalculateRect(e, canvasEntity, *_cameraModule);
        const f32 scaleFactor = ui_utility::CalculateCanvasScaleFactor(canvas, *_cameraModule);
        auto pixelRect = math::Rect {
            logicalRect.Min * scaleFactor,
            logicalRect.Max * scaleFactor
        };
        pixelRect = ui_utility::ApplyAspectPreservation(pixelRect, image);

        const auto pixelSize = pixelRect.GetSize();
        if (pixelSize.x <= 0.0f || pixelSize.y <= 0.0f) continue;

        auto model = ui_utility::BuildModelMatrix(pixelRect, GET(e, rei::ui::RectTransform), GET(e, rei::Transform));
        model = glm::scale(model, glm::vec3(0.5f, 0.5f, 1.0f));

        const Shader& shader = image.GetRenderMaterial().GetShader();
        shader.SetViewMatrices(projection, view, model);
        image.GetRenderMaterial().Use();
        _quadVertexData.Render();
    }
}
