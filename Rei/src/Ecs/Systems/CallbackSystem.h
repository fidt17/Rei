#pragma once
#include "Ecs/System.h"

namespace rei::ecs
{
    class CallbackSystem final : public System
    {
    public:
        CallbackSystem(const std::shared_ptr<EcsRegistry>& ecs, const std::shared_ptr<FilterProvider>& filters,
                       const std::function<void()>& callback);

        void OnUpdate() override;

    private:
        std::function<void()> _callback;
    };
}
