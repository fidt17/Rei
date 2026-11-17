#pragma once
#include "Common/Primitives.h"
#include <memory>
#include <atomic>
#include <future>
#include <set>
#include <vector>

namespace rei
{
    struct SimulatedInputEvent
    {
        enum Type { Move, ButtonDown, ButtonUp, KeyDown, KeyUp, Scroll, Wait };
        Type Kind = Wait;
        f64 X = 0;
        f64 Y = 0;
        i32 Code = 0;
    };

    // One bounded sequence, advanced on the engine thread after polling GLFW.
    struct SimulatedInput
    {
        std::vector<SimulatedInputEvent> Events;
        std::promise<i32> Completion;
        std::atomic<bool> Cancelled = false;
        size_t Next = 0;
        i32 Width = 0;
        i32 Height = 0;
        std::set<i32> Keys;
        std::set<i32> Buttons;
    };
}
