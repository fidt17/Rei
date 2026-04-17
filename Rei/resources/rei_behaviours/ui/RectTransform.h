#pragma once

#include "Common/Math/Vector3.h"

namespace rei::ui
{
    class RectTransform : public Behaviour
    {
        BEHAVIOUR_BODY(RectTransform)

        SERIALIZE math::Vector3 _anchorMin = math::Vector3(0.5f, 0.5f, 0);
        SERIALIZE math::Vector3 _anchorMax = math::Vector3(0.5f, 0.5f, 0);
        SERIALIZE math::Vector3 _pivot = math::Vector3(0.5f, 0.5f, 0);
        SERIALIZE math::Vector3 _anchoredPosition = math::Vector3(0, 0, 0);
        SERIALIZE math::Vector3 _sizeDelta = math::Vector3(100, 100, 0);
    };
}

EXPORT_COMPONENT(rei::ui::RectTransform)
