#pragma once

namespace rei::editor
{
    class TransformationControlsModule : ecs::IEcsModule
    {
    public:
        void AddSystems(std::shared_ptr<ecs::World> world) override;
    };
}
