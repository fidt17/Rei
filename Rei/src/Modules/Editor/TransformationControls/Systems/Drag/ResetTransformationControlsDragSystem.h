#pragma once
#include "Ecs/System.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"

namespace rei::editor
{
    class EditorActionStore;

    class ResetTransformationControlsDragSystem : public ecs::System
    {
    public:
        ResetTransformationControlsDragSystem(const std::shared_ptr<ecs::World>& ecsWorld, const std::shared_ptr<EditorActionStore>& actions);

        void OnUpdate() override;

    private:
        std::shared_ptr<ecs::Filter> _controlFilter;
        std::shared_ptr<EditorActionStore> _actions;

        void RecordMove(const TransformationControl& control) const;
        void ResetDragState(TransformationControl& control) const;
    };
}
