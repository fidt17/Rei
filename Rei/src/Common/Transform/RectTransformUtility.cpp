#include "pch.h"

#include "RectTransformUtility.h"

#include <algorithm>

#include "rei_behaviours/transformation/Transform.h"
#include "rei_behaviours/ui/Canvas.h"
#include "rei_behaviours/ui/Image.h"
#include "rei_behaviours/ui/RectTransform.h"

namespace rei::ui_utility
{
    namespace
    {
        math::Rect CalculateChildRect(const math::Rect& parentRect, const rei::ui::RectTransform& rectTransform)
        {
            const glm::vec2 parentSize = parentRect.GetSize();
            const auto& anchorMinValue = rectTransform.GetAnchorMin();
            const auto& anchorMaxValue = rectTransform.GetAnchorMax();
            const auto& pivotValue = rectTransform.GetPivot();
            const auto& anchoredPositionValue = rectTransform.GetAnchoredPosition();
            const auto& sizeDeltaValue = rectTransform.GetSizeDelta();

            const glm::vec2 anchorMin(anchorMinValue.x, anchorMinValue.y);
            const glm::vec2 anchorMax(anchorMaxValue.x, anchorMaxValue.y);
            const glm::vec2 pivot(pivotValue.x, pivotValue.y);
            const glm::vec2 anchoredPosition(anchoredPositionValue.x, anchoredPositionValue.y);
            const glm::vec2 sizeDelta(sizeDeltaValue.x, sizeDeltaValue.y);

            const glm::vec2 anchorMinPosition = parentRect.Min + parentSize * anchorMin;
            const glm::vec2 anchorMaxPosition = parentRect.Min + parentSize * anchorMax;
            const glm::vec2 anchorCenter = (anchorMinPosition + anchorMaxPosition) * 0.5f;
            const glm::vec2 size = (anchorMaxPosition - anchorMinPosition) + sizeDelta;
            const glm::vec2 center = anchorCenter + anchoredPosition;

            return {
                center - pivot * size,
                center + (glm::vec2(1.0f, 1.0f) - pivot) * size
            };
        }
    }

    ecs::Entity FindCanvasEntity(ecs::Entity entity)
    {
        ECS_WORLD(rei::GetInternalWorld())

        auto current = entity;
        while (!IS_DEAD(current))
        {
            if (HAS(current, rei::ui::Canvas))
            {
                return current;
            }

            if (!HAS(current, rei::Transform))
            {
                break;
            }

            current = GET(current, rei::Transform).GetParent();
        }

        return rei::ecs::NULL_ENTITY;
    }

    f32 CalculateCanvasScaleFactor(const rei::ui::Canvas& canvas, const rei::render::CameraModule& cameraModule)
    {
        if (canvas.GetScaleMode() == rei::ui::ConstantPixelSize)
        {
            return 1.0f;
        }

        const auto referenceResolution = canvas.GetReferenceResolution();
        const f32 referenceWidth = referenceResolution.x <= 0.0f ? 1.0f : referenceResolution.x;
        const f32 referenceHeight = referenceResolution.y <= 0.0f ? 1.0f : referenceResolution.y;

        const f32 widthScale = static_cast<f32>(cameraModule.GetWidth()) / referenceWidth;
        const f32 heightScale = static_cast<f32>(cameraModule.GetHeight()) / referenceHeight;
        const f32 match = std::clamp(canvas.GetMatchWidthOrHeight(), 0.0f, 1.0f);
        return (std::max)(0.0001f, glm::mix(widthScale, heightScale, match));
    }

    math::Rect GetCanvasRect(const rei::ui::Canvas& canvas, const rei::render::CameraModule& cameraModule)
    {
        const f32 scaleFactor = CalculateCanvasScaleFactor(canvas, cameraModule);
        return {
            glm::vec2(0.0f, 0.0f),
            glm::vec2(
                static_cast<f32>(cameraModule.GetWidth()) / scaleFactor,
                static_cast<f32>(cameraModule.GetHeight()) / scaleFactor)
        };
    }

    math::Rect CalculateRect(const ecs::Entity entity, const ecs::Entity canvasEntity, const rei::render::CameraModule& cameraModule)
    {
        ECS_WORLD(rei::GetInternalWorld())

        math::Rect currentRect = GetCanvasRect(GET(canvasEntity, rei::ui::Canvas), cameraModule);
        std::vector<ecs::Entity> hierarchy;

        auto current = entity;
        while (!IS_DEAD(current) && current != canvasEntity)
        {
            hierarchy.push_back(current);
            if (!HAS(current, rei::Transform))
            {
                break;
            }

            current = GET(current, rei::Transform).GetParent();
        }

        std::reverse(hierarchy.begin(), hierarchy.end());
        for (const auto hierarchyEntity : hierarchy)
        {
            if (!HAS(hierarchyEntity, rei::ui::RectTransform)) continue;
            currentRect = CalculateChildRect(currentRect, GET(hierarchyEntity, rei::ui::RectTransform));
        }

        return currentRect;
    }

    math::Rect ApplyAspectPreservation(const math::Rect& rect, const rei::ui::Image& image)
    {
        if (!image.PreserveAspect())
        {
            return rect;
        }

        const auto& texture = image.GetTexture();
        if (!texture.IsLoaded() || texture->GetWidth() <= 0 || texture->GetHeight() <= 0)
        {
            return rect;
        }

        const glm::vec2 rectSize = rect.GetSize();
        if (rectSize.x <= 0.0f || rectSize.y <= 0.0f)
        {
            return rect;
        }

        const f32 textureAspect = static_cast<f32>(texture->GetWidth()) / static_cast<f32>(texture->GetHeight());
        glm::vec2 size = rectSize;
        if (textureAspect > rectSize.x / rectSize.y)
        {
            size.y = rectSize.x / textureAspect;
        }
        else
        {
            size.x = rectSize.y * textureAspect;
        }

        const glm::vec2 center = rect.GetCenter();
        return {
            center - size * 0.5f,
            center + size * 0.5f
        };
    }

    glm::mat4 BuildModelMatrix(const math::Rect& rect)
    {
        auto model = glm::mat4(1.0f);
        model = glm::translate(model, glm::vec3(rect.GetCenter(), 0.0f));
        model = glm::scale(model, glm::vec3(rect.GetSize(), 1.0f));
        model = glm::scale(model, glm::vec3(0.5f, 0.5f, 1.0f));
        return model;
    }
}
