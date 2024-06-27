#pragma once
#include "Modules/Input/Input.h"

namespace rei::render
{
    class FlyCameraSystem : public ecs::System
    {
    public:
        FlyCameraSystem(const std::shared_ptr<ecs::EcsRegistry>& ecs, const std::shared_ptr<ecs::FilterProvider>& filters, const std::shared_ptr<input::Input>& input);

        void OnUpdate() override;

    private:
        std::shared_ptr<ecs::Filter> _f;
        std::shared_ptr<input::Input> _input;
    };
}
