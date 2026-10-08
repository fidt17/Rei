#pragma once
#include <memory>
#include <vector>
#include "Common/Primitives.h"

namespace rei::ecs { class World; }

namespace rei::api
{
    class EditorSelectionRelay
    {
    public:
        void SetCallback(void (*callback)(const char*, i32)) { _callback = callback; }
        void Capture(const std::shared_ptr<ecs::World>& world);

    private:
        std::vector<i32> _selected;
        void (*_callback)(const char*, i32) = nullptr;
    };
}
