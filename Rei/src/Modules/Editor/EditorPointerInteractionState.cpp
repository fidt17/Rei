#include "pch.h"
#include "EditorPointerInteractionState.h"

namespace rei::editor
{
    void EditorPointerInteractionState::BeginSelectionCandidate(const ecs::Entity entity, const bool additiveSelection)
    {
        _selectionCandidate = entity;
        _hasSelectionCandidate = true;
        _additiveSelection = additiveSelection;
        _consumed = false;
    }

    void EditorPointerInteractionState::Consume()
    {
        _consumed = true;
    }

    void EditorPointerInteractionState::Reset()
    {
        _selectionCandidate = ecs::NULL_ENTITY;
        _hasSelectionCandidate = false;
        _additiveSelection = false;
        _consumed = false;
    }

    bool EditorPointerInteractionState::HasSelectionCandidate()
    {
        return _hasSelectionCandidate;
    }

    bool EditorPointerInteractionState::IsConsumed()
    {
        return _consumed;
    }

    bool EditorPointerInteractionState::IsAdditiveSelection()
    {
        return _additiveSelection;
    }

    ecs::Entity EditorPointerInteractionState::GetSelectionCandidate()
    {
        return _selectionCandidate;
    }
}
