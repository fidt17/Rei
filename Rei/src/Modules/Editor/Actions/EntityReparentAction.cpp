#include "pch.h"
#include "EntityReparentAction.h"
#include <utility>

namespace rei::editor
{
    EntityReparentAction::State::State(const Transform& transform) :
        Parent(transform.GetParent()), Position(transform.GetLocalPosition()), Scale(transform.GetLocalScale()), Rotation(transform.GetLocalRotation())
    {
    }

    EntityReparentAction::EntityReparentAction(ecs::ComponentRef<Transform> target, State before, State after, std::vector<EntityOrderAction::Change> orders) :
        _target(target), _before(before), _after(after), _orders(std::move(orders))
    {
    }

    bool EntityReparentAction::Undo() { return Apply(true); }
    bool EntityReparentAction::Redo() { return Apply(false); }

    bool EntityReparentAction::Apply(const bool undo)
    {
        if (_target.IsNull()) return false;
        const auto& state = undo ? _before : _after;
        ECS_WORLD(GetInternalWorld())
        if (state.Parent != ecs::NULL_ENTITY && (IS_DEAD(state.Parent) || !HAS(state.Parent, Transform))) return false;
        for (const auto& change : _orders)
        {
            if (change.Target.IsNull()) return false;
        }

        auto& transform = _target.Get();
        transform.SetParent(state.Parent);
        transform.GetLocalPosition() = state.Position;
        transform.GetLocalScale() = state.Scale;
        transform.SetRotation(state.Rotation);
        for (const auto& change : _orders)
        {
            change.Target.Get().SetChildOrder(undo ? change.Before : change.After);
        }
        return true;
    }
}
