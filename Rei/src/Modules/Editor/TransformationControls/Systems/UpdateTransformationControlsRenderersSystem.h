#pragma once
#include "Ecs/System.h"
#include "Modules/Editor/TransformationControls/TransformationControlScaleArrow.h"
#include "Modules/Render/Color/Color.h"

namespace rei::editor
{
    struct TransformationControl;
    struct TransformationControlMovementArrow;

    class UpdateTransformationControlsRenderersSystem : public ecs::System
    {
    public:
        explicit UpdateTransformationControlsRenderersSystem(const std::shared_ptr<ecs::World>& ecsWorld);

        void OnUpdate() override;

    private:
        void UpdateMovementControls(const TransformationControl& control, bool isVisible, bool showDepthControls) const;
        void UpdateScaleControls(const TransformationControl& control, bool isVisible, bool showDepthControls) const;
        void UpdateRotationControls(const TransformationControl& control, bool isVisible, bool showDepthControls) const;
        void UpdateControlPart(const ecs::Entity& e, bool visible, const render::Color& defaultColor, const render::Color& highlightColor) const;
        void SetControlPartVisible(const ecs::Entity& e, bool visible) const;
        void UpdateRenderer(const ecs::Entity& e, const render::Color& defaultColor, const render::Color& highlightColor) const;
        
    private:
        std::shared_ptr<ecs::Filter> _controlFilter;

        render::Color red = render::Color::FromHex("#bf212f");
        render::Color redBright = render::Color::FromHex("#D52635");
        render::Color green = render::Color::FromHex("#27b376");
        render::Color greenBright = render::Color::FromHex("#2FCE89");
        render::Color blue = render::Color::FromHex("#264b96");
        render::Color blueBright = render::Color::FromHex("#2E5BB4");
        render::Color plane = render::Color(1.0f, 1.0f, 1.0f, 0.25f);
        render::Color planeBright = render::Color(1.0f, 1.0f, 1.0f, 0.4f);
        render::Color grey = render::Color(0.7f, 0.7f, 0.7f, 1.0f);
        render::Color greyBright = render::Color(0.8f, 0.8f, 0.8f);
    };
}
