#include "pch.h"
#include "EntityReparentAction.h"
#include <utility>

namespace rei::editor
{
    EntityReparentAction::State::State(const Transform& transform) :
        Parent(transform.GetParent()), Position(transform.GetLocalPosition()), Scale(transform.GetLocalScale()), Rotation(transform.GetLocalRotation())
    {
        ECS_WORLD(GetInternalWorld())
        if (!HAS(transform.GetEntity(), ui::RectTransform)) return;
        const auto& rect = GET(transform.GetEntity(), ui::RectTransform);
        Rect = RectTransformChangeAction::State::Capture(rect);
    }

    EntityReparentAction::EntityReparentAction(ecs::ComponentRef<Transform> target, State before, State after, std::vector<EntityOrderAction::Change> orders) :
        EntityReparentAction(std::vector<Change>{{target, before, after}}, std::move(orders))
    {
    }

    EntityReparentAction::EntityReparentAction(std::vector<Change> changes, std::vector<EntityOrderAction::Change> orders) :
        _changes(std::move(changes)), _orders(std::move(orders))
    {
    }

    bool EntityReparentAction::Undo() { return Apply(true); }
    bool EntityReparentAction::Redo() { return Apply(false); }

    bool EntityReparentAction::Apply(const bool undo)
    {
        if (_changes.empty()) return false;
        ECS_WORLD(GetInternalWorld())
        for (const auto& change : _changes)
        {
            if (change.Target.IsNull()) return false;
            const auto& state = undo ? change.Before : change.After;
            if (state.Rect && !HAS(change.Target.Get().GetEntity(), ui::RectTransform)) return false;
            if (state.Parent != ecs::NULL_ENTITY && (IS_DEAD(state.Parent) || !HAS(state.Parent, Transform))) return false;
        }
        for (const auto& change : _orders)
        {
            if (change.Target.IsNull()) return false;
        }

        for (const auto& change : _changes)
        {
            const auto& state = undo ? change.Before : change.After;
            auto& transform = change.Target.Get();
            transform.SetParent(state.Parent);
            transform.GetLocalPosition() = state.Position;
            transform.GetLocalScale() = state.Scale;
            transform.SetRotation(state.Rotation);
            if (state.Rect)
            {
                auto& rect = GET(transform.GetEntity(), ui::RectTransform);
                state.Rect->ApplyTo(rect, RectTransformChangeAction::Fields::Rectangle);
            }
        }
        for (const auto& change : _orders)
        {
            change.Target.Get().SetChildOrder(undo ? change.Before : change.After);
        }
        return true;
    }
}
