#pragma once

class BehaviourModule : public rei::ecs::IEcsModule
{
public:
    void Configure(std::shared_ptr<rei::ecs::World>) override;
};
