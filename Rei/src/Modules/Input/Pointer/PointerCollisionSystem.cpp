#include "pch.h"
#include "Common/Profiling/ProfileMarkers.h"
#include "PointerCollisionSystem.h"
#include "PointerCollisionStateUtility.h"

#include "Modules/Components/ActiveTag.h"
#include "Modules/Input/Input.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/transformation/Transform.h"
#include "rei_behaviours/ui/RectTransform.h"

namespace rei::input
{
    PointerCollisionSystem::PointerCollisionSystem(const std::shared_ptr<ecs::World>& world) : System(world)
    {
        _entities = FILTER(physics::PointerCollisionListener);
    }

    void PointerCollisionSystem::OnUpdate()
    {
        REI_PROFILE_SCOPE(profiling::markers::PICK_3D.Id);
        const auto camera = render::Camera::GetMainCamera();
        f32 xPos = 0.0f, yPos = 0.0f;
        Input::GetMousePosition(xPos, yPos);
        const auto ray = camera.IsNull() ? math::Ray({}, {}) : camera.Get().GetScreenPointToRay(xPos, yPos);

        FOR(e, _entities)
        {
            // UI owns final hit state for RectTransform listeners; do not overwrite it here.
            if (HAS(e, ui::RectTransform)) continue;
            auto& listener = GET(e, physics::PointerCollisionListener);
            bool isInside = false;
            if (!camera.IsNull() && HAS(e, ActiveTag) && HAS(e, Transform) && listener.Collider)
            {
                profiling::Count(profiling::markers::PICK_CANDIDATES.Id);
                isInside = listener.Collider->Intersect(ray, GET(e, Transform).CalculateWorldModelMatrix(), listener.CollisionPoint);
            }
            UpdatePointerCollisionState(listener, isInside);
        }
    }
}
