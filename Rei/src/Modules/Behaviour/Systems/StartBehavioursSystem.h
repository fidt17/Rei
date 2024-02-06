#pragma once

class StartBehavioursSystem final : public rei::ecs::System
{
private:
    std::shared_ptr<rei::ecs::Filter> _f;
    
public:
    StartBehavioursSystem(const std::shared_ptr<rei::ecs::EcsRegistry>& ecs, const std::shared_ptr<rei::ecs::FilterProvider>& filters);

    void OnUpdate() override;
};
