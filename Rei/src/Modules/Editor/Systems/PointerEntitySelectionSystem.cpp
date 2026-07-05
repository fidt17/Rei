#include "pch.h"

#include "PointerEntitySelectionSystem.h"

#include "Modules/Editor/EditorPointerInteractionState.h"
#include "Modules/Editor/EntitySelectionUtility.h"
#include "Modules/Components/ActiveTag.h"
#include "Modules/Editor/Components/SelectableByPointerTag.h"
#include "Modules/Editor/Components/SelectedTag.h"
#include "Modules/Editor/Components/SelectionByPointerBlockerTag.h"
#include "Modules/Input/Input.h"
#include "Modules/Physics/PointerCollisionListener.h"
#include "Modules/Render/UI/UIUtility.h"

namespace rei::editor
{
    namespace
    {
        bool IsAdditiveSelectionRequested()
        {
            return Input::IsKeyDown(GLFW_KEY_LEFT_CONTROL) ||
                   Input::IsKeyDown(GLFW_KEY_RIGHT_CONTROL) ||
                   Input::IsKeyDown(GLFW_KEY_LEFT_SHIFT) ||
                   Input::IsKeyDown(GLFW_KEY_RIGHT_SHIFT);
        }
    }

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
        if (Input::IsMouseButtonPressed(GLFW_MOUSE_BUTTON_LEFT))
        {
            if (IsSelectionBlocked())
            {
                EditorPointerInteractionState::Reset();
                return;
            }

            EditorPointerInteractionState::BeginSelectionCandidate(FindSelectionCandidate(), IsAdditiveSelectionRequested());
            return;
        }

        if (!Input::IsMouseButtonReleased(GLFW_MOUSE_BUTTON_LEFT)) return;

        if (EditorPointerInteractionState::HasSelectionCandidate() && !EditorPointerInteractionState::IsConsumed())
        {
            CommitSelection(EditorPointerInteractionState::GetSelectionCandidate(), EditorPointerInteractionState::IsAdditiveSelection());
        }

        EditorPointerInteractionState::Reset();
    }

    ecs::Entity PointerEntitySelectionSystem::FindSelectionCandidate() const
    {
        ecs::Entity selectedCandidate = ecs::NULL_ENTITY;
        FOR(e, _checkEntities)
        {
            const auto& listener = GET(e, physics::PointerCollisionListener);
            if (!listener.IsInside) continue;

            if (render::ui_render_utility::IsUiEntity(e))
            {
                if (render::ui_render_utility::IsHigherUiEntity(e, selectedCandidate))
                {
                    selectedCandidate = e;
                }
                continue;
            }

            if (IS_DEAD(selectedCandidate))
            {
                selectedCandidate = e;
            }
        }

        return selectedCandidate;
    }

    void PointerEntitySelectionSystem::CommitSelection(const ecs::Entity selectedCandidate, const bool additiveSelection) const
    {
        if (!IS_DEAD(selectedCandidate))
        {
            if (additiveSelection && HAS(selectedCandidate, SelectedTag)) return;

            selection_utility::Select(_ecsWorld, selectedCandidate, !additiveSelection);
            return;
        }

        if (additiveSelection) return;
        ResetAllEntitiesSelection();
    }

    bool PointerEntitySelectionSystem::IsSelectionBlocked() const
    {
        FOR(e, _blockSelectionEntities)
        {
            if (GET(e, physics::PointerCollisionListener).IsInside) return true;
        }

        return false;
    }
}
