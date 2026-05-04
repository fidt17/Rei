#include "pch.h"
#include "UIPointerCollisionSystem.h"

#include "Common/Transform/RectTransformUtility.h"
#include "Modules/Components/ActiveTag.h"
#include "Modules/Input/Input.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/transformation/Transform.h"
#include "rei_behaviours/ui/Canvas.h"
#include "rei_behaviours/ui/Image.h"
#include "rei_behaviours/ui/RectTransform.h"

namespace rei::editor
{
    UIPointerCollisionSystem::UIPointerCollisionSystem(const std::shared_ptr<ecs::World>& world) : System(world)
    {
        _entities = FILTER(physics::PointerCollisionListener, Transform, ui::RectTransform, ui::Image, ActiveTag);
    }

    void UIPointerCollisionSystem::OnUpdate()
    {
        const auto mainCamera = render::Camera::GetMainCamera();
        if (mainCamera.IsNull()) return;

        i32 width = 1;
        i32 height = 1;
        mainCamera.Get().GetOutputSize(width, height);

        f32 xPos = 0.0f;
        f32 yPos = 0.0f;
        Input::GetMousePosition(xPos, yPos);
        const math::Vector2 screenPoint(xPos, static_cast<f32>(height) - yPos);

        FOR(e, _entities)
        {
            auto& listener = GET(e, physics::PointerCollisionListener);
            listener.DidEnter = false;
            listener.DidExit = false;

            const auto& image = GET(e, ui::Image);
            bool isInside = false;
            if (image.IsEnabled() && image.IsRaycastTarget())
            {
                const auto canvasEntity = ui_utility::FindCanvasEntity(e);
                if (!IS_DEAD(canvasEntity) && HAS(canvasEntity, ui::Canvas))
                {
                    const auto& canvas = GET(canvasEntity, ui::Canvas);
                    const auto logicalRect = ui_utility::CalculateRect(e, canvasEntity, width, height);
                    const f32 scaleFactor = ui_utility::CalculateCanvasScaleFactor(canvas, width, height);
                    auto pixelRect = math::Rect {
                        logicalRect.Min * scaleFactor,
                        logicalRect.Max * scaleFactor
                    };
                    pixelRect = ui_utility::ApplyAspectPreservation(pixelRect, image);
                    isInside = ui_utility::IsScreenPointInside(screenPoint, pixelRect, GET(e, ui::RectTransform), GET(e, Transform));
                }
            }

            if (isInside)
            {
                if (!listener.IsInside)
                {
                    listener.DidEnter = true;
                }

                listener.IsInside = true;
                listener.CollisionPoint = math::Vector3(screenPoint.x, screenPoint.y, 0.0f);
            }
            else
            {
                if (listener.IsInside)
                {
                    listener.DidExit = true;
                }

                listener.IsInside = false;
            }
        }
    }
}
