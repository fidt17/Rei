#pragma once

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
        int Code;
        int Mods;
        float MouseX;
        float MouseY;
    };
}
