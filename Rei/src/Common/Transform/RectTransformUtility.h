#pragma once

#include "Common/Math/Rect.h"
#include "Modules/Render/RenderScenario/CameraModule.h"

namespace rei::ui
{
    class Canvas;
    class Image;
    class RectTransform;
}

namespace rei
{
    class Transform;
}

namespace rei::ui_utility
{
    ecs::Entity FindCanvasEntity(ecs::Entity entity);
    f32 CalculateCanvasScaleFactor(const ui::Canvas& canvas, i32 width, i32 height);
    f32 CalculateCanvasScaleFactor(const ui::Canvas& canvas, const render::CameraModule& cameraModule);
    math::Rect GetCanvasRect(const ui::Canvas& canvas, i32 width, i32 height);
    math::Rect GetCanvasRect(const ui::Canvas& canvas, const render::CameraModule& cameraModule);
    math::Rect CalculateRect(ecs::Entity entity, ecs::Entity canvasEntity, i32 width, i32 height);
    math::Rect CalculateRect(ecs::Entity entity, ecs::Entity canvasEntity, const render::CameraModule& cameraModule);
    math::Rect ApplyAspectPreservation(const math::Rect& rect, const ui::Image& image);
    math::Vector2 GetPivotPosition(const math::Rect& rect, const ui::RectTransform& rectTransform);
    glm::mat4 BuildModelMatrix(const math::Rect& rect);
    glm::mat4 BuildModelMatrix(const math::Rect& rect, const Transform& transform);
    glm::mat4 BuildModelMatrix(const math::Rect& rect, const ui::RectTransform& rectTransform, const Transform& transform);
    bool IsScreenPointInside(const math::Vector2& point, const math::Rect& rect, const ui::RectTransform& rectTransform, const Transform& transform);
}
