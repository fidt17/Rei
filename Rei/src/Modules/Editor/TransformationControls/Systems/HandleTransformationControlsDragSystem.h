#pragma once
#include "Ecs/System.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"

namespace rei::editor
{
    class HandleTransformationControlsDragSystem : public ecs::System
    {
    public:
        explicit HandleTransformationControlsDragSystem(const std::shared_ptr<ecs::World>& ecsWorld);

        void OnUpdate() override;

    private:
        void ResetDragState(TransformationControl& control) const;
        void HandleMovementDrag(TransformationControl& control) const;
        void HandleScaleDrag(TransformationControl& control) const;
        
    private:
        std::shared_ptr<ecs::Filter> _controlFilter;
    };
}
