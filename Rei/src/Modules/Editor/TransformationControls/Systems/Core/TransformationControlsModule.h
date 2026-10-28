#pragma once

namespace rei::editor
{
    class EditorActionStore;

    class TransformationControlsModule : ecs::IEcsModule
    {
    public:
        explicit TransformationControlsModule(const std::shared_ptr<EditorActionStore>& actions) : _actions(actions) {}
        void AddSystems(std::shared_ptr<ecs::World> world) override;

    private:
        std::shared_ptr<EditorActionStore> _actions;
    };
}
