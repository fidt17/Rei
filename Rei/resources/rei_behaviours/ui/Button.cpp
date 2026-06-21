#include "pch.h"

#include "Button.h"

#include "Modules/Input/Input.h"
#include "Modules/Physics/PointerCollisionListener.h"

namespace rei::ui
{
    void Button::Init()
    {
        EnsurePointerCollisionListener();
        EnsureTargetImage();
        ApplyVisualState();
    }

    void Button::AfterREI_SET()
    {
        EnsurePointerCollisionListener();
        EnsureTargetImage();
        ApplyVisualState();
    }

    void Button::Update()
    {
        EnsurePointerCollisionListener();
        EnsureTargetImage();

        if (!_interactable)
        {
            _isPointerInside = false;
            _isPressed = false;
            ApplyVisualState();
            return;
        }

        ECS_WORLD(GetInternalWorld())
        const auto& listener = GET(GetEntity(), physics::PointerCollisionListener);
        const bool isPointerInside = listener.IsInside;

        if (!_isPointerInside && isPointerInside)
        {
            _isPointerInside = true;
            PointerEnteredEvent();
        }

        if (_isPointerInside && !isPointerInside)
        {
            _isPointerInside = false;
            PointerExitedEvent();
        }

        if (isPointerInside && Input::IsMouseButtonPressed(GLFW_MOUSE_BUTTON_LEFT))
        {
            _isPressed = true;
            PressedEvent();
        }

        if (_isPressed && Input::IsMouseButtonReleased(GLFW_MOUSE_BUTTON_LEFT))
        {
            ReleasedEvent();
            if (isPointerInside)
            {
                ClickedEvent();
            }

            _isPressed = false;
        }

        ApplyVisualState();
    }

    bool Button::IsPointerInside() const
    {
        return _isPointerInside;
    }

    bool Button::IsPressed() const
    {
        return _isPressed;
    }

    bool Button::IsInteractable() const
    {
        return _interactable;
    }

    void Button::SetInteractable(const bool value)
    {
        _interactable = value;
        if (!_interactable)
        {
            _isPointerInside = false;
            _isPressed = false;
        }

        ApplyVisualState();
    }

    ecs::ComponentRef<Image>& Button::GetTargetImage()
    {
        return _targetImage;
    }

    void Button::SetTargetImage(const ecs::ComponentRef<Image>& image)
    {
        _targetImage = image;
        ApplyVisualState();
    }

    void Button::EnsureTargetImage()
    {
        if (!_targetImage.IsNull()) return;

        ECS_WORLD(GetInternalWorld())
        const auto entity = GetEntity();
        if (IS_DEAD(entity) || !HAS(entity, Image)) return;

        _targetImage = GET_REF(entity, Image);
    }

    void Button::EnsurePointerCollisionListener() const
    {
        ECS_WORLD(GetInternalWorld())
        const auto entity = GetEntity();
        if (IS_DEAD(entity)) return;

        GET(entity, physics::PointerCollisionListener);
    }

    void Button::ApplyVisualState()
    {
        if (!_changeImageColor) return;
        if (_targetImage.IsNull()) return;

        _targetImage.Get().SetColor(GetCurrentVisualColor());
    }

    render::Color Button::GetCurrentVisualColor() const
    {
        if (!_interactable) return _disabledColor;
        if (_isPressed && _isPointerInside) return _pressedColor;
        if (_isPointerInside) return _hoverColor;

        return _normalColor;
    }
}
