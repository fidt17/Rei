#include "pch.h"
#include "PointerCollisionSystem.h"

#include "Modules/Components/ActiveTag.h"
#include "Modules/Input/Input.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/transformation/Transform.h"

namespace rei::input
{
    PointerCollisionSystem::PointerCollisionSystem(const std::shared_ptr<ecs::World>& world) : System(world)
    {
        _entities = FILTER(physics::PointerCollisionListener, Transform, ActiveTag);
    }

    void PointerCollisionSystem::OnUpdate()
    {
        const auto camera = render::Camera::GetMainCamera();
        if (camera.IsNull()) return;

        f32 xPos, yPos;
        Input::GetMousePosition(xPos, yPos);
        const auto ray = camera.Get().GetScreenPointToRay(xPos, yPos);

        FOR(e, _entities)
        {
            auto& transform = GET(e, Transform);
            auto& listener = GET(e, physics::PointerCollisionListener);

            listener.DidEnter = false;
            listener.DidExit = false;
            if (!listener.Collider) continue;

            if (listener.Collider->Intersect(ray, transform.CalculateWorldModelMatrix(), listener.CollisionPoint))
            {
                if (!listener.IsInside)
                {
                    listener.DidEnter = true;
                }

                listener.IsInside = true;
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
