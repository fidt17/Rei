#include "pch.h"
#include "PointerCollisionSystem.h"

#include "Modules/Input/Input.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/transformation/Transform.h"

rei::physics::PointerCollisionSystem::PointerCollisionSystem(const std::shared_ptr<ecs::EcsRegistry>& ecs, const std::shared_ptr<ecs::FilterProvider>& filters)
    : System(ecs, filters),
      _entities(filters->Get<PointerCollisionListener, transformation::Transform>())
{
}

void rei::physics::PointerCollisionSystem::OnUpdate()
{
    const auto camera = render::Camera::GetMainCamera();
    if (camera.IsNull()) return;

    f32 xPos, yPos;
    Input::GetMousePosition(xPos, yPos);
    const auto ray = camera.Get().GetScreenPointToRay(xPos, yPos);

    FOR(e, _entities)
    {
        auto& transform = GET(e, transformation::Transform);
        auto& listener = GET(e, PointerCollisionListener);

        listener.DidEnter = false;
        listener.DidExit = false;

        if (listener.Collider && listener.Collider->Intersect(ray, transform.CalculateModelMatrix(), listener.CollisionPoint))
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
