#include "pch.h"

#include "PointerEntitySelectionSystem.h"
#include "Common/Profiling/ProfileMarkers.h"
#include "Modules/Editor/Components/EditorSelectionCollider.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/transformation/Transform.h"

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
        EditorPointerInteractionState::Reset();
        _checkEntities = FILTER(SelectableByPointerTag, ActiveTag);
        _blockSelectionEntities = FILTER(SelectionByPointerBlockerTag, physics::PointerCollisionListener, ActiveTag);
    }

    void PointerEntitySelectionSystem::ResetAllEntitiesSelection() const
    {
        selection_utility::Reset(_ecsWorld);
    }

    void PointerEntitySelectionSystem::OnUpdate()
    {
        const auto camera = render::Camera::GetMainCamera();
        const auto cameraEntity = camera.IsNull() ? ecs::NULL_ENTITY : camera.Get().GetEntity();
        if (camera.IsNull() || (EditorPointerInteractionState::HasSelectionCandidate() && _candidateCamera != cameraEntity))
        {
            EditorPointerInteractionState::Reset();
            _candidateCamera = ecs::NULL_ENTITY;
            if (camera.IsNull()) return;
        }
        if (!Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_LEFT) && !Input::IsMouseButtonReleased(GLFW_MOUSE_BUTTON_LEFT))
        {
            EditorPointerInteractionState::Reset();
            _candidateCamera = ecs::NULL_ENTITY;
            return;
        }

        if (Input::IsMouseButtonPressed(GLFW_MOUSE_BUTTON_LEFT))
        {
            if (IsSelectionBlocked())
            {
                EditorPointerInteractionState::Reset();
                return;
            }

            _candidateCamera = cameraEntity;
            EditorPointerInteractionState::BeginSelectionCandidate(FindSelectionCandidate(), IsAdditiveSelectionRequested());
            return;
        }

        if (!Input::IsMouseButtonReleased(GLFW_MOUSE_BUTTON_LEFT)) return;

        if (EditorPointerInteractionState::HasSelectionCandidate() && !EditorPointerInteractionState::IsConsumed())
        {
            const auto candidate = EditorPointerInteractionState::GetSelectionCandidate();
            if (IsCandidateValid(candidate)) CommitSelection(candidate, EditorPointerInteractionState::IsAdditiveSelection());
        }

        EditorPointerInteractionState::Reset();
        _candidateCamera = ecs::NULL_ENTITY;
    }

    ecs::Entity PointerEntitySelectionSystem::FindSelectionCandidate() const
    {
        REI_PROFILE_SCOPE(profiling::markers::PICK_SELECTION.Id);
        const auto camera = render::Camera::GetMainCamera();
        if (camera.IsNull()) return ecs::NULL_ENTITY;
        f32 x = 0.0f, y = 0.0f;
        Input::GetMousePosition(x, y);
        const auto ray = camera.Get().GetScreenPointToRay(x, y);
        ecs::Entity selectedCandidate = ecs::NULL_ENTITY;
        FOR(e, _checkEntities)
        {
            bool isInside = false;
            if (!render::ui_render_utility::IsUiEntity(e) && HAS(e, EditorSelectionCollider) && HAS(e, Transform))
            {
                const auto& collider = GET(e, EditorSelectionCollider).Collider;
                if (collider)
                {
                    profiling::Count(profiling::markers::PICK_SELECTION_CANDIDATES.Id);
                    profiling::Count(profiling::markers::PICK_CANDIDATES.Id);
                    math::Vector3 point;
                    isInside = collider->Intersect(ray, GET(e, Transform).CalculateWorldModelMatrix(), point);
                }
            }
            else if (HAS(e, physics::PointerCollisionListener))
            {
                isInside = GET(e, physics::PointerCollisionListener).IsInside;
            }
            if (!isInside) continue;

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

    bool PointerEntitySelectionSystem::IsCandidateValid(const ecs::Entity candidate) const
    {
        if (candidate == ecs::NULL_ENTITY) return true;
        if (IS_DEAD(candidate) || !HAS(candidate, ActiveTag) || !HAS(candidate, SelectableByPointerTag) || !HAS(candidate, Transform)) return false;
        // Release validates lifecycle only. Loaded geometry/pose changes keep the press hit.
        if (render::ui_render_utility::IsUiEntity(candidate)) return HAS(candidate, physics::PointerCollisionListener);
        if (HAS(candidate, EditorSelectionCollider))
        {
            const auto& collider = GET(candidate, EditorSelectionCollider).Collider;
            return collider && collider->IsAvailable();
        }
        if (!HAS(candidate, physics::PointerCollisionListener)) return false;
        const auto& collider = GET(candidate, physics::PointerCollisionListener).Collider;
        return collider && collider->IsAvailable();
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
