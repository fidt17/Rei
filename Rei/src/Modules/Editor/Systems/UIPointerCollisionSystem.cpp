#include "pch.h"
#include "UIPointerCollisionSystem.h"

#include "Common/Transform/RectTransformUtility.h"
#include "Engine/Services.h"
#include "Modules/Components/ActiveTag.h"
#include "Modules/Input/Input.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/transformation/Transform.h"
#include "rei_behaviours/ui/Button.h"
#include "rei_behaviours/ui/Canvas.h"
#include "rei_behaviours/ui/Image.h"
#include "rei_behaviours/ui/RectTransform.h"
#include "rei_behaviours/ui/Text.h"

namespace rei::editor
{
    namespace
    {
        ecs::Entity FindNearestButtonEntity(const ecs::Entity sourceEntity)
        {
            ECS_WORLD(GetInternalWorld())

            auto currentEntity = sourceEntity;
            while (!IS_DEAD(currentEntity))
            {
                if (HAS(currentEntity, ui::Button)) return currentEntity;
                if (!HAS(currentEntity, Transform)) return ecs::NULL_ENTITY;

                currentEntity = GET(currentEntity, Transform).GetParent();
            }

            return ecs::NULL_ENTITY;
        }

        bool IsRectHit(const ecs::Entity entity, const math::Vector2& screenPoint, const i32 width, const i32 height, math::Rect pixelRect)
        {
            ECS_WORLD(GetInternalWorld())

            const auto canvasEntity = ui_utility::FindCanvasEntity(entity);
            if (IS_DEAD(canvasEntity) || !HAS(canvasEntity, ui::Canvas)) return false;

            const auto& canvas = GET(canvasEntity, ui::Canvas);
            const f32 scaleFactor = ui_utility::CalculateCanvasScaleFactor(canvas, width, height);
            pixelRect.Min *= scaleFactor;
            pixelRect.Max *= scaleFactor;

            return ui_utility::IsScreenPointInside(screenPoint, pixelRect, GET(entity, ui::RectTransform), GET(entity, Transform));
        }

        math::Rect CalculateLogicalRect(const ecs::Entity entity, const i32 width, const i32 height)
        {
            const auto canvasEntity = ui_utility::FindCanvasEntity(entity);
            if (IS_DEAD(canvasEntity) || !HAS(canvasEntity, ui::Canvas)) return {};

            return ui_utility::CalculateRect(entity, canvasEntity, width, height);
        }

        bool IsImageHit(const ecs::Entity entity, const math::Vector2& screenPoint, const i32 width, const i32 height)
        {
            const auto& image = GET(entity, ui::Image);
            if (!image.IsEnabled() || !image.IsRaycastTarget()) return false;

            const auto logicalRect = CalculateLogicalRect(entity, width, height);
            auto pixelRect = ui_utility::ApplyAspectPreservation(logicalRect, image);
            return IsRectHit(entity, screenPoint, width, height, pixelRect);
        }

        bool IsTextHit(const ecs::Entity entity, const math::Vector2& screenPoint, const i32 width, const i32 height)
        {
            const auto& text = GET(entity, ui::Text);
            if (!text.IsEnabled() || !text.IsRaycastTarget()) return false;

            return IsRectHit(entity, screenPoint, width, height, CalculateLogicalRect(entity, width, height));
        }

        bool IsUiHit(const ecs::Entity entity, const math::Vector2& screenPoint, const i32 width, const i32 height)
        {
            ECS_WORLD(GetInternalWorld())

            if (HAS(entity, ui::Image) && IsImageHit(entity, screenPoint, width, height)) return true;
            if (HAS(entity, ui::Text) && IsTextHit(entity, screenPoint, width, height)) return true;

            return false;
        }

        void SetPointerState(const ecs::Entity entity, const bool isInside, const math::Vector2& screenPoint)
        {
            ECS_WORLD(GetInternalWorld())

            auto& listener = GET(entity, physics::PointerCollisionListener);
            listener.DidEnter = false;
            listener.DidExit = false;

            if (isInside)
            {
                listener.DidEnter = !listener.IsInside;
                listener.IsInside = true;
                listener.CollisionPoint = math::Vector3(screenPoint.x, screenPoint.y, 0.0f);
                return;
            }

            listener.DidExit = listener.IsInside;
            listener.IsInside = false;
        }
    }

    UIPointerCollisionSystem::UIPointerCollisionSystem(const std::shared_ptr<ecs::World>& world) : System(world)
    {
        _entities = FILTER(physics::PointerCollisionListener, Transform, ui::RectTransform, ActiveTag);
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

        std::unordered_set<ecs::Entity> hitEntities;
        const bool bubbleToButton = GetEngine().IsPlaymode();

        FOR(e, _entities)
        {
            if (!IsUiHit(e, screenPoint, width, height)) continue;

            hitEntities.insert(e);
            if (!bubbleToButton) continue;

            const auto buttonEntity = FindNearestButtonEntity(e);
            if (!IS_DEAD(buttonEntity))
            {
                hitEntities.insert(buttonEntity);
            }
        }

        FOR(e, _entities)
        {
            SetPointerState(e, hitEntities.contains(e), screenPoint);
        }
    }
}
