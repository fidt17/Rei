#include "pch.h"

#include "UIRenderModule.h"

#include "Api/EditorApi.h"
#include "glm/ext/matrix_clip_space.hpp"
#include "Common/Transform/RectTransformUtility.h"
#include "Modules/Components/ActiveTag.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "Modules/Render/Mesh/VertexObjects/QuadVertexObject.h"
#include "Modules/Render/Shaders/Shader.h"
#include "rei_behaviours/transformation/Transform.h"
#include "rei_behaviours/ui/Canvas.h"
#include "rei_behaviours/ui/Image.h"
#include "rei_behaviours/ui/RectTransform.h"

namespace rei::render
{
    UIRenderModule::UIRenderModule(const std::shared_ptr<CameraModule>& cameraModule)
        : _cameraModule(cameraModule)
    {
        _uiRenderingEnabledSetHandle = GetEditorEventsRelay().UiRenderingEnabledReceivedEvent.append([this](const bool value)
        {
            HandleUiRenderingEnabledSetEvent(value);
        });
    }

    UIRenderModule::~UIRenderModule()
    {
        GetEditorEventsRelay().UiRenderingEnabledReceivedEvent.remove(_uiRenderingEnabledSetHandle);
    }

    void UIRenderModule::Setup()
    {
        EnsureQuadModel();
    }

    void UIRenderModule::Render() const
    {
        if (!_isEnabled) return;
        if (!_quadModel.IsLoaded()) return;

        ECS_WORLD(rei::GetInternalWorld())
        const auto images = FILTER(rei::ui::Image, ActiveTag);
        const glm::mat4 projection = glm::ortho(0.0f, static_cast<f32>(_cameraModule->GetWidth()), 0.0f, static_cast<f32>(_cameraModule->GetHeight()), -1.0f, 1.0f);
        const glm::mat4 view = glm::mat4(1.0f);

        FOR(e, images)
        {
            auto& image = GET(e, rei::ui::Image);
            if (!image.IsEnabled()) continue;
            if (!HAS(e, rei::ui::RectTransform)) continue;
            if (!HAS(e, rei::Transform)) continue;

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

            const math::Vector2 pixelSize = pixelRect.GetSize();
            if (pixelSize.x <= 0.0f || pixelSize.y <= 0.0f) continue;

            const auto& rectTransform = GET(e, rei::ui::RectTransform);
            const auto& material = image.GetRenderMaterial();
            const Shader& shader = material.GetShader();
            shader.SetViewMatrices(projection, view, ui_utility::BuildModelMatrix(pixelRect, rectTransform, GET(e, rei::Transform)));
            material.Use();

            for (const auto& mesh : _quadModel->GetMeshes())
            {
                mesh.Render();
            }
        }
    }

    void UIRenderModule::HandleUiRenderingEnabledSetEvent(const bool value)
    {
        _isEnabled = value;
    }

    void UIRenderModule::EnsureQuadModel()
    {
        if (_quadModel.IsLoaded()) return;
        _quadModel = GetAssetManager().CreateAsset<Model>("UI Shared Quad", QuadVertexObject(1.0f, 1.0f).GenerateMesh());
    }
}
