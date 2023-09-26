#pragma once

namespace rei::internal::update_loop
{
    class UpdateLoopModule : public ecs::IEcsModule
    {
    public:
        void Configure(std::shared_ptr<ecs::World>) override;
    };
}
