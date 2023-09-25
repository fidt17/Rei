#pragma once

namespace rei::internal::update_loop
{
    class InvokeUpdateCallbackSystem final : public ecs::System
    {
    public:
        InvokeUpdateCallbackSystem(const std::shared_ptr<ecs::EcsRegistry>& ecs, const std::shared_ptr<ecs::FilterProvider>& filters);

        void OnUpdate() override;

    private:
        const std::shared_ptr<ecs::Filter> _f;
    };
}
