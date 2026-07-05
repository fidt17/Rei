#pragma once
#include "Ecs/System.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"

namespace rei::editor
{
    class HandleTransformationControlsScaleDragSystem : public ecs::System
    {
    public:
        explicit HandleTransformationControlsScaleDragSystem(const std::shared_ptr<ecs::World>& ecsWorld);

        void OnUpdate() override;

    private:
        bool HandleUiScaleDrag(TransformationControl& control) const;
        void HandleScaleDrag(TransformationControl& control) const;

    private:
        std::shared_ptr<ecs::Filter> _controlFilter;
    };
}
