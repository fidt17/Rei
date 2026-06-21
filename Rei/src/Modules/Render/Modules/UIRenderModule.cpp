#include "pch.h"

#include "UIRenderModule.h"

#include <algorithm>
#include <array>

#include "Api/EditorApi.h"
#include "glm/ext/matrix_clip_space.hpp"
#include "Common/Transform/RectTransformUtility.h"
#include "glad/glad.h"
#include "Modules/Components/ActiveTag.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "Modules/Render/Mesh/VertexObjects/QuadVertexObject.h"
#include "Modules/Render/Shaders/Shader.h"
#include "Modules/Render/UI/Text/Font.h"
#include "Modules/Render/UI/UIUtility.h"
#include "rei_behaviours/transformation/Transform.h"
#include "rei_behaviours/ui/Canvas.h"
#include "rei_behaviours/ui/Image.h"
#include "rei_behaviours/ui/RectTransform.h"
#include "rei_behaviours/ui/Text.h"

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
        DisposeTextRenderObjects();
        GetEditorEventsRelay().UiRenderingEnabledReceivedEvent.remove(_uiRenderingEnabledSetHandle);
    }

    void UIRenderModule::Setup()
    {
        EnsureQuadModel();
        EnsureTextRenderObjects();
        _textShader = GetAssetManager().GetById<Shader>(REI_SHADER_TEXT_ASSET_ID);
    }

    void UIRenderModule::Render() const
    {
        if (!_isEnabled) return;

        const glm::mat4 projection = glm::ortho(0.0f, static_cast<f32>(_cameraModule->GetWidth()), 0.0f, static_cast<f32>(_cameraModule->GetHeight()), -1.0f, 1.0f);
        const glm::mat4 view = glm::mat4(1.0f);

        RenderUiItems(CollectUiRenderItems(), projection, view);
    }

    std::vector<UIRenderModule::UiRenderItem> UIRenderModule::CollectUiRenderItems() const
    {
        ECS_WORLD(rei::GetInternalWorld())

        std::vector<ecs::Entity> canvases;
        const auto canvasFilter = FILTER(rei::ui::Canvas, ActiveTag);
        FOR(canvasEntity, canvasFilter)
        {
            if (!HAS(canvasEntity, rei::Transform)) continue;
            canvases.push_back(canvasEntity);
        }

        std::ranges::sort(canvases, [](const ecs::Entity a, const ecs::Entity b)
        {
            return ui_render_utility::BuildHierarchySortKey(a) < ui_render_utility::BuildHierarchySortKey(b);
        });

        std::vector<UiRenderItem> renderItems;
        for (const auto canvasEntity : canvases)
        {
            CollectUiRenderItems(canvasEntity, renderItems);
        }

        return renderItems;
    }

    void UIRenderModule::CollectUiRenderItems(const ecs::Entity entity, std::vector<UiRenderItem>& renderItems) const
    {
        ECS_WORLD(rei::GetInternalWorld())

        if (IS_DEAD(entity) || !HAS(entity, rei::Transform)) return;

        if (HAS(entity, ActiveTag))
        {
            if (HAS(entity, rei::ui::Image))
            {
                renderItems.push_back({entity, UiRenderItemType::Image});
            }

            if (HAS(entity, rei::ui::Text))
            {
                renderItems.push_back({entity, UiRenderItemType::Text});
            }
        }

        const auto children = GET(entity, rei::Transform).GetChildren();
        for (const auto child : children)
        {
            CollectUiRenderItems(child, renderItems);
        }
    }

    void UIRenderModule::RenderUiItems(const std::vector<UiRenderItem>& renderItems, const glm::mat4& projection, const glm::mat4& view) const
    {
        for (const auto& renderItem : renderItems)
        {
            switch (renderItem.Type)
            {
                case UiRenderItemType::Image:
                    DrawImage(renderItem.Entity, projection, view);
                    break;
                case UiRenderItemType::Text:
                    DrawUiText(renderItem.Entity, projection, view);
                    break;
            }
        }
    }

    void UIRenderModule::DrawImage(const ecs::Entity entity, const glm::mat4& projection, const glm::mat4& view) const
    {
        if (!_quadModel.IsLoaded()) return;

        ECS_WORLD(rei::GetInternalWorld())

        auto& image = GET(entity, rei::ui::Image);
        if (!image.IsEnabled()) return;
        if (!HAS(entity, rei::ui::RectTransform)) return;
        if (!HAS(entity, rei::Transform)) return;

        const auto canvasEntity = ui_utility::FindCanvasEntity(entity);
        if (IS_DEAD(canvasEntity) || !HAS(canvasEntity, rei::ui::Canvas)) return;

        const auto& canvas = GET(canvasEntity, rei::ui::Canvas);
        const auto logicalRect = ui_utility::CalculateRect(entity, canvasEntity, *_cameraModule);
        const f32 scaleFactor = ui_utility::CalculateCanvasScaleFactor(canvas, *_cameraModule);
        auto pixelRect = math::Rect {
            logicalRect.Min * scaleFactor,
            logicalRect.Max * scaleFactor
        };
        pixelRect = ui_utility::ApplyAspectPreservation(pixelRect, image);

        const math::Vector2 pixelSize = pixelRect.GetSize();
        if (pixelSize.x <= 0.0f || pixelSize.y <= 0.0f) return;

        const auto& rectTransform = GET(entity, rei::ui::RectTransform);
        const auto& material = image.GetRenderMaterial();
        const Shader& shader = material.GetShader();
        shader.SetViewMatrices(projection, view, ui_utility::BuildModelMatrix(pixelRect, rectTransform, GET(entity, rei::Transform)));
        material.Use();

        for (const auto& mesh : _quadModel->GetMeshes())
        {
            mesh.Render();
        }
    }

    void UIRenderModule::DrawUiText(const ecs::Entity entity, const glm::mat4& projection, const glm::mat4& view) const
    {
        if (!_textShader.IsLoaded()) return;
        if (_textVao == 0 || _textVbo == 0) return;

        ECS_WORLD(rei::GetInternalWorld())

        const auto model = glm::mat4(1.0f);
        const auto& shader = *_textShader.Get();
        const auto& text = GET(entity, rei::ui::Text);
        if (!text.IsEnabled()) return;
        if (!HAS(entity, rei::ui::RectTransform)) return;
        if (!HAS(entity, rei::Transform)) return;

        const auto& font = text.GetFont();
        if (!font.IsLoaded()) return;

        const auto canvasEntity = ui_utility::FindCanvasEntity(entity);
        if (IS_DEAD(canvasEntity) || !HAS(canvasEntity, rei::ui::Canvas)) return;

        const auto& canvas = GET(canvasEntity, rei::ui::Canvas);
        const auto logicalRect = ui_utility::CalculateRect(entity, canvasEntity, *_cameraModule);
        const f32 scaleFactor = ui_utility::CalculateCanvasScaleFactor(canvas, *_cameraModule);
        const auto pixelRect = math::Rect {
            logicalRect.Min * scaleFactor,
            logicalRect.Max * scaleFactor
        };

        const math::Vector2 pixelSize = pixelRect.GetSize();
        if (pixelSize.x <= 0.0f || pixelSize.y <= 0.0f) return;

        shader.SetViewMatrices(projection, view, model);
        shader.SetInt("_MainTex", 0);
        shader.SetColor("_Color", text.GetColor());

        glDisable(GL_DEPTH_TEST);
        glEnable(GL_BLEND);
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);

        glActiveTexture(GL_TEXTURE0);
        glBindVertexArray(_textVao);

        const f32 fontScale = text.GetSize() / static_cast<f32>(font->GetPixelHeight());
        const f32 lineHeight = text.GetLineHeight();
        const f32 startX = pixelRect.Min.x;
        f32 x = startX;
        f32 y = pixelRect.Max.y - text.GetSize();

        for (const char character : text.GetValue())
        {
            if (character == '\n')
            {
                x = startX;
                y -= lineHeight;
                continue;
            }

            const auto glyphKey = static_cast<u8>(character);
            if (!font->HasGlyph(glyphKey)) continue;

            const auto& glyph = font->GetGlyph(glyphKey);
            if (glyph.TextureId != 0)
            {
                const f32 glyphX = x + static_cast<f32>(glyph.BearingX) * fontScale;
                const f32 glyphY = y - static_cast<f32>(glyph.Height - glyph.BearingY) * fontScale;
                const f32 glyphWidth = static_cast<f32>(glyph.Width) * fontScale;
                const f32 glyphHeight = static_cast<f32>(glyph.Height) * fontScale;
                DrawGlyphQuad(glyphX, glyphY, glyphWidth, glyphHeight, glyph.TextureId);
            }

            x += glyph.GetAdvancePixels() * fontScale;
        }

        glBindVertexArray(0);
        glBindTexture(GL_TEXTURE_2D, 0);
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

    void UIRenderModule::EnsureTextRenderObjects()
    {
        if (_textVao != 0 && _textVbo != 0) return;

        glGenVertexArrays(1, &_textVao);
        glGenBuffers(1, &_textVbo);

        glBindVertexArray(_textVao);
        glBindBuffer(GL_ARRAY_BUFFER, _textVbo);
        glBufferData(GL_ARRAY_BUFFER, sizeof(f32) * 6 * 8, nullptr, GL_DYNAMIC_DRAW);
        glEnableVertexAttribArray(0);
        glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, sizeof(f32) * 8, nullptr);
        glEnableVertexAttribArray(1);
        glVertexAttribPointer(1, 3, GL_FLOAT, GL_FALSE, sizeof(f32) * 8, reinterpret_cast<void*>(sizeof(f32) * 3));
        glEnableVertexAttribArray(2);
        glVertexAttribPointer(2, 2, GL_FLOAT, GL_FALSE, sizeof(f32) * 8, reinterpret_cast<void*>(sizeof(f32) * 6));

        glBindBuffer(GL_ARRAY_BUFFER, 0);
        glBindVertexArray(0);
    }

    void UIRenderModule::DisposeTextRenderObjects()
    {
        if (_textVbo != 0)
        {
            glDeleteBuffers(1, &_textVbo);
            _textVbo = 0;
        }

        if (_textVao != 0)
        {
            glDeleteVertexArrays(1, &_textVao);
            _textVao = 0;
        }
    }

    void UIRenderModule::DrawGlyphQuad(const f32 x, const f32 y, const f32 width, const f32 height, const u32 textureId) const
    {
        const std::array<f32, 48> vertices = {
            x, y + height, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
            x, y, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f,
            x + width, y, 0.0f, 0.0f, 0.0f, 1.0f, 1.0f, 1.0f,
            x, y + height, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
            x + width, y, 0.0f, 0.0f, 0.0f, 1.0f, 1.0f, 1.0f,
            x + width, y + height, 0.0f, 0.0f, 0.0f, 1.0f, 1.0f, 0.0f,
        };

        glBindTexture(GL_TEXTURE_2D, textureId);
        glBindBuffer(GL_ARRAY_BUFFER, _textVbo);
        glBufferSubData(GL_ARRAY_BUFFER, 0, sizeof(vertices), vertices.data());
        glDrawArrays(GL_TRIANGLES, 0, 6);
    }
}
