#pragma once
#include "Ecs/System.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"

namespace rei::editor
{
    class TransformationControlActivationSystem : public ecs::System
    {
    public:
        explicit TransformationControlActivationSystem(const std::shared_ptr<ecs::World>& ecsWorld);
        
        void DisableMovementControls(const TransformationControl& transformationControl) const;
        void DisableScaleControls(const TransformationControl& transformationControl) const;
        void DisableRotationControls(const TransformationControl& transformationControl) const;
        
        void EnableMovementControls(const TransformationControl& transformationControl) const;
        void EnableScaleControls(const TransformationControl& transformationControl) const;
        void EnableRotationControls(const TransformationControl& transformationControl) const;

        void OnUpdate() override;

    private:
        std::shared_ptr<ecs::Filter> _controlFilter;
    };
}
