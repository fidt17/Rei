#pragma once

#include "Modules/Render/Color/Color.h"
#include "Modules/Render/Textures/Texture.h"

namespace rei::ui
{
    class Image : public Behaviour
    {
        BEHAVIOUR_BODY(Image)

        SERIALIZE assets::AssetRef<render::Texture> _texture;
        SERIALIZE render::Color _color = render::Color::White();
        SERIALIZE bool _preserveAspect = false;
        SERIALIZE bool _raycastTarget = true;
    };
}

EXPORT_COMPONENT(rei::ui::Image)
