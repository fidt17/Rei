#pragma once
#include "Ecs/System.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"

namespace rei::editor
{
    class ResetTransformationControlsDragSystem : public ecs::System
    {
    public:
        explicit ResetTransformationControlsDragSystem(const std::shared_ptr<ecs::World>& ecsWorld);

        void OnUpdate() override;

    private:
        void ResetDragState(TransformationControl& control) const;

    private:
        std::shared_ptr<ecs::Filter> _controlFilter;
    };
}
