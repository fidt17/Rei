#pragma once

class UpdateBehavioursSystem final : public rei::ecs::System
{
private:
    std::shared_ptr<rei::ecs::Filter> _f;
    
public:
    UpdateBehavioursSystem(const std::shared_ptr<rei::ecs::EcsRegistry>& ecs, const std::shared_ptr<rei::ecs::FilterProvider>& filters);

    void OnUpdate() override;
};
