#pragma once
#include "Common/Primitives.h"

namespace rei::api
{
    enum EditorInputEventType
    {
        KeyDown = 0,
        KeyUp = 1,
        MouseButtonDown = 2,
        MouseButtonUp = 3,
    };

    struct EditorInputEvent
    {
        EditorInputEventType Type;
        i32 Code;
        i32 Mods;
        f32 MouseX;
        f32 MouseY;
    };
}
