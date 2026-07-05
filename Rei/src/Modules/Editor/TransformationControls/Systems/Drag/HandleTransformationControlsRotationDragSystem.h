#pragma once
#include "Ecs/System.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"

namespace rei::editor
{
    class HandleTransformationControlsRotationDragSystem : public ecs::System
    {
    public:
        explicit HandleTransformationControlsRotationDragSystem(const std::shared_ptr<ecs::World>& ecsWorld);

        void OnUpdate() override;

    private:
        bool HandleUiRotationDrag(TransformationControl& control) const;
        void HandleRotationDrag(TransformationControl& control) const;

    private:
        std::shared_ptr<ecs::Filter> _controlFilter;
    };
}
