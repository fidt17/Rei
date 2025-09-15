#include "pch.h"
#include "PointerEntitySelectionSystem.h"

#include "SelectableByPointerTag.h"
#include "../../../resources/rei_behaviours/render/RenderOutlineTag.h"
#include "../../../resources/rei_behaviours/render/camera/Camera.h"
#include "Modules/Input/Input.h"
#include "Modules/Physics/PointerCollisionListener.h"

rei::editor::PointerEntitySelectionSystem::PointerEntitySelectionSystem(const std::shared_ptr<ecs::EcsRegistry>& ecs,
                                                                        const std::shared_ptr<ecs::FilterProvider>& filters): System(ecs, filters),
    _checkEntities(filters->Get<physics::PointerCollisionListener, SelectableByPointerTag>()),
    _selectedEntities(GetInternalWorld().GetFiltersRegistry()->Get<SelectedTag>())
{
}

void rei::editor::PointerEntitySelectionSystem::ResetAllEntitiesSelection() const
{
    FOR(e, _selectedEntities)
    {
        DEL(e, rei::editor::SelectedTag);
        DEL(e, rei::render::RenderOutlineTag);
    }
}

void rei::editor::PointerEntitySelectionSystem::OnUpdate()
{
    if (!Input::IsMouseButtonReleased(GLFW_MOUSE_BUTTON_LEFT)) return;

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
