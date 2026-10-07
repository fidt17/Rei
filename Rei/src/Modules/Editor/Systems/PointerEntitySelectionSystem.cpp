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
        _sourceRevision = Input::GetSourceRevision();
        _checkEntities = FILTER(SelectableByPointerTag, ActiveTag);
        _blockSelectionEntities = FILTER(SelectionByPointerBlockerTag, physics::PointerCollisionListener, ActiveTag);
    }

    void PointerEntitySelectionSystem::ResetAllEntitiesSelection() const
    {
        selection_utility::Reset(_ecsWorld);
    }

    void PointerEntitySelectionSystem::OnUpdate()
    {
        if (_sourceRevision != Input::GetSourceRevision())
        {
            _sourceRevision = Input::GetSourceRevision();
            EditorPointerInteractionState::Reset();
            ResetPickCycle();
        }
        const auto camera = render::Camera::GetMainCamera();
        const auto cameraEntity = camera.IsNull() ? ecs::NULL_ENTITY : camera.Get().GetEntity();
        if (camera.IsNull() || (EditorPointerInteractionState::HasSelectionCandidate() && _candidateCamera != cameraEntity))
        {
            EditorPointerInteractionState::Reset();
            _candidateCamera = ecs::NULL_ENTITY;
            ResetPickCycle();
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
            if (IsCandidateValid(candidate))
            {
                CommitSelection(candidate, EditorPointerInteractionState::IsAdditiveSelection());
                RememberPick(candidate);
            }
        }

        EditorPointerInteractionState::Reset();
        _candidateCamera = ecs::NULL_ENTITY;
    }

    void PointerEntitySelectionSystem::ResetPickCycle()
    {
        _pickHistory.clear();
        _pressHits.clear();
        _restartCycle = false;
    }

    void PointerEntitySelectionSystem::RememberPick(const ecs::Entity entity)
    {
        if (_restartCycle) _pickHistory.clear();
        std::erase_if(_pickHistory, [&](const ecs::Entity previous) { return !_pressHits.contains(previous); });
        if (entity != ecs::NULL_ENTITY) _pickHistory.insert(entity);
    }

    ecs::Entity PointerEntitySelectionSystem::FindSelectionCandidate()
    {
        REI_PROFILE_SCOPE(profiling::markers::PICK_SELECTION.Id);
        struct Hit
        {
            ecs::Entity Entity;
            f32 Depth;
            bool IsUi;
            std::vector<i32> UiOrder;
        };
        _pressHits.clear();
        _restartCycle = false;
        const auto camera = render::Camera::GetMainCamera();
        if (camera.IsNull()) return ecs::NULL_ENTITY;
        f32 x = 0, y = 0;
        Input::GetMousePosition(x, y);
        const auto ray = camera.Get().GetScreenPointToRay(x, y);
        const auto view = camera.Get().GetViewMatrix();
        std::vector<Hit> hits;
        FOR(e, _checkEntities)
        {
            if (!HAS(e, Transform)) continue;
            const auto isUi = render::ui_render_utility::IsUiEntity(e);
            const auto model = GET(e, Transform).CalculateWorldModelMatrix();
            bool isInside = false;
            if (!isUi && HAS(e, EditorSelectionCollider))
            {
                const auto& collider = GET(e, EditorSelectionCollider).Collider;
                if (collider)
                {
                    profiling::Count(profiling::markers::PICK_SELECTION_CANDIDATES.Id);
                    profiling::Count(profiling::markers::PICK_CANDIDATES.Id);
                    math::Vector3 point;
                    isInside = collider->Intersect(ray, model, point);
                }
            }
            else if (HAS(e, physics::PointerCollisionListener))
            {
                isInside = GET(e, physics::PointerCollisionListener).IsInside;
            }
            if (!isInside) continue;
            // Camera-space origin depth is enough for ordering; geometry only answers hit/miss.
            const auto depth = isUi ? 0 : -(view * model[3]).z;
            if (!std::isfinite(depth)) continue;
            hits.push_back({e, depth, isUi, isUi ? render::ui_render_utility::BuildHierarchySortKey(e) : std::vector<i32>{}});
            _pressHits.insert(e);
        }
        std::sort(hits.begin(), hits.end(), [](const Hit& a, const Hit& b)
        {
            if (a.IsUi != b.IsUi) return a.IsUi;
            if (a.IsUi && a.UiOrder != b.UiOrder) return a.UiOrder > b.UiOrder;
            if (a.Depth != b.Depth) return a.Depth < b.Depth;
            if (a.Entity.Id != b.Entity.Id) return a.Entity.Id < b.Entity.Id;
            return a.Entity.Generation < b.Entity.Generation;
        });
        for (const auto& hit : hits)
        {
            if (!HAS(hit.Entity, SelectedTag) && !_pickHistory.contains(hit.Entity)) return hit.Entity;
        }
        // Every current hit was selected or visited. Begin another round on successful release.
        _restartCycle = true;
        for (const auto& hit : hits)
        {
            if (!HAS(hit.Entity, SelectedTag)) return hit.Entity;
        }
        return hits.empty() ? ecs::NULL_ENTITY : hits.front().Entity;
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
