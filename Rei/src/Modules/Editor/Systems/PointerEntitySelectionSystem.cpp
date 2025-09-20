#include "pch.h"

#include "PointerEntitySelectionSystem.h"

#include "Modules/Editor/Components/SelectableByPointerTag.h"
#include "Modules/Editor/Components/SelectedTag.h"
#include "Modules/Editor/Components/SelectionByPointerBlockerTag.h"
#include "Modules/Input/Input.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "rei_behaviours/render/RenderOutlineTag.h"

namespace rei::editor
{
    PointerEntitySelectionSystem::PointerEntitySelectionSystem(const std::shared_ptr<ecs::EcsRegistry>& ecs,
                                                               const std::shared_ptr<ecs::FilterProvider>& filters): System(ecs, filters),
        _checkEntities(filters->Get<physics::PointerCollisionListener, SelectableByPointerTag>()),
        _selectedEntities(filters->Get<SelectedTag>()),
        _blockSelectionEntities(filters->Get<SelectionByPointerBlockerTag, physics::PointerCollisionListener>())
    {
    }

    void PointerEntitySelectionSystem::ResetAllEntitiesSelection() const
    {
        FOR(e, _selectedEntities)
        {
            DEL(e, rei::editor::SelectedTag);
            DEL(e, rei::render::RenderOutlineTag);
        }
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
                ResetAllEntitiesSelection();

                GET(e, rei::editor::SelectedTag);
                GET(e, render::RenderOutlineTag);
                return;
            }
        }

        ResetAllEntitiesSelection();
    }
}
