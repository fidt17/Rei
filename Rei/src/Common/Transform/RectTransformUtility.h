#pragma once

#include "Common/Math/Rect.h"
#include "Modules/Render/RenderScenario/CameraModule.h"

namespace rei::ui
{
    class Canvas;
    class Image;
    class RectTransform;
}

namespace rei::ui_utility
{
    ecs::Entity FindCanvasEntity(ecs::Entity entity);
    f32 CalculateCanvasScaleFactor(const ui::Canvas& canvas, const render::CameraModule& cameraModule);
    math::Rect GetCanvasRect(const ui::Canvas& canvas, const render::CameraModule& cameraModule);
    math::Rect CalculateRect(ecs::Entity entity, ecs::Entity canvasEntity, const render::CameraModule& cameraModule);
    math::Rect ApplyAspectPreservation(const math::Rect& rect, const ui::Image& image);
    glm::mat4 BuildModelMatrix(const math::Rect& rect);
}
