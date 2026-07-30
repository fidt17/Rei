#pragma once

namespace rei::editor
{
    class EditorPointerInteractionState
    {
    public:
        static void BeginSelectionCandidate(ecs::Entity entity, bool additiveSelection);
        static void Consume();
        static void Reset();

        static bool HasSelectionCandidate();
        static bool IsConsumed();
        static bool IsAdditiveSelection();
        static ecs::Entity GetSelectionCandidate();

    private:
        inline static ecs::Entity _selectionCandidate = ecs::NULL_ENTITY;
        inline static bool _hasSelectionCandidate = false;
        inline static bool _additiveSelection = false;
        inline static bool _consumed = false;
    };
}
