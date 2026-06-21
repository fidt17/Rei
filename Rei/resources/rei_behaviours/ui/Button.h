#pragma once

#include "Ecs/ComponentRef.h"
#include "Modules/Render/Color/Color.h"
#include "rei_behaviours/ui/Image.h"

namespace rei::ui
{
    class Button : public Behaviour
    {
        REQUIRE_COMPONENT(RectTransform)
        REQUIRE_COMPONENT(Image)
        BEHAVIOUR_BODY(Button)

        SERIALIZE ecs::ComponentRef<Image> _targetImage;
        SERIALIZE bool _interactable = true;
        SERIALIZE bool _changeImageColor = true;
        SERIALIZE render::Color _normalColor = render::Color::White();
        SERIALIZE render::Color _hoverColor = render::Color(0.9f, 0.9f, 0.9f, 1.0f);
        SERIALIZE render::Color _pressedColor = render::Color(0.75f, 0.75f, 0.75f, 1.0f);
        SERIALIZE render::Color _disabledColor = render::Color(0.5f, 0.5f, 0.5f, 1.0f);

        bool _isPointerInside = false;
        bool _isPressed = false;

    public:
        REI_EVENT(void) ClickedEvent;
        REI_EVENT(void) PointerEnteredEvent;
        REI_EVENT(void) PointerExitedEvent;
        REI_EVENT(void) PressedEvent;
        REI_EVENT(void) ReleasedEvent;

        REI_API void Init() override;
        REI_API void AfterREI_SET() override;
        REI_API void Update() override;

        REI_API bool IsPointerInside() const;
        REI_API bool IsPressed() const;
        REI_API bool IsInteractable() const;
        REI_API void SetInteractable(bool value);
        REI_API ecs::ComponentRef<Image>& GetTargetImage();
        REI_API void SetTargetImage(const ecs::ComponentRef<Image>& image);

    private:
        void EnsureTargetImage();
        void EnsurePointerCollisionListener() const;
        void ApplyVisualState();
        render::Color GetCurrentVisualColor() const;
    };
}

EXPORT_COMPONENT(rei::ui::Button)
