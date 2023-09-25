#pragma once

namespace rei::internal::update_loop
{
    struct UpdateCallback
    {
        std::function<void()> Callback;
    };
}
