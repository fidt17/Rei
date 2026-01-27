#pragma once
#include "Ecs/System.h"
#include "Modules/Editor/TransformationControls/TransformationControlScaleArrow.h"
#include "Modules/Render/Color/Color.h"

namespace rei::editor
{
    struct TransformationControlMovementArrow;

    class UpdateTransformationControlsRenderersSystem : public ecs::System
    {
    public:
        explicit UpdateTransformationControlsRenderersSystem(const std::shared_ptr<ecs::World>& ecsWorld);

        void OnUpdate() override;

    private:
        void UpdateRenderer(const ecs::Entity& e, const render::Color& defaultColor, const render::Color& highlightColor) const;
        
    private:
        std::shared_ptr<ecs::Filter> _controlFilter;

        render::Color red = render::Color::FromHex("#bf212f");
        render::Color redBright = render::Color::FromHex("#D52635");
        render::Color green = render::Color::FromHex("#27b376");
        render::Color greenBright = render::Color::FromHex("#2FCE89");
        render::Color blue = render::Color::FromHex("#264b96");
        render::Color blueBright = render::Color::FromHex("#2E5BB4");
        render::Color grey = render::Color(0.7, 0.7, 0.7, 1);
        render::Color greyBright = render::Color(0.8, 0.8, 0.8);
    };
}
