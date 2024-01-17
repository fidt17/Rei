#pragma once

namespace rei::internal::update_loop
{
    struct UpdateCallback
    {
        std::function<void()> Callback;
    };
}

EXPORT_COMPONENT(rei::internal::update_loop::UpdateCallback);
