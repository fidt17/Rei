#include "pch.h"

#include "PointerEntitySelectionSystem.h"

#include "Modules/Editor/EntitySelectionUtility.h"
#include "Modules/Components/ActiveTag.h"
#include "Modules/Editor/Components/SelectableByPointerTag.h"
#include "Modules/Editor/Components/SelectedTag.h"
#include "Modules/Editor/Components/SelectionByPointerBlockerTag.h"
#include "Modules/Input/Input.h"
#include "Modules/Physics/PointerCollisionListener.h"

namespace rei::editor
{
    PointerEntitySelectionSystem::PointerEntitySelectionSystem(const std::shared_ptr<ecs::World>& world): System(world)
    {
        _checkEntities = FILTER(physics::PointerCollisionListener, SelectableByPointerTag, ActiveTag);
        _blockSelectionEntities = FILTER(SelectionByPointerBlockerTag, physics::PointerCollisionListener, ActiveTag);
    }

    void PointerEntitySelectionSystem::ResetAllEntitiesSelection() const
    {
        selection_utility::Reset(_ecsWorld);
    }

    void PointerEntitySelectionSystem::OnUpdate()
    {
        if (!Input::IsMouseButtonPressed(GLFW_MOUSE_BUTTON_LEFT)) return;

        // if pointer is over any blocker entity -> return
        FOR(e, _blockSelectionEntities)
        {
            if (GET(e, physics::PointerCollisionListener).IsInside)
            {
                return;
            }
        }

        // todo: should sort entities by distance from camera to minimize cases when further object gets selected first
        FOR(e, _checkEntities)
        {
            if (HAS(e, SelectedTag)) continue;

            const auto& listener = GET(e, physics::PointerCollisionListener);

            if (listener.IsInside)
            {
                selection_utility::Select(_ecsWorld, e);
                return;
            }
        }

        ResetAllEntitiesSelection();
    }
}
