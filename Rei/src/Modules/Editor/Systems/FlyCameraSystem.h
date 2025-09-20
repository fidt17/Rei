#pragma once

namespace rei::editor
{
    class FlyCameraSystem : public ecs::System
    {
    public:
        FlyCameraSystem(const std::shared_ptr<ecs::EcsRegistry>& ecs, const std::shared_ptr<ecs::FilterProvider>& filters);

        void MoveCamera(rei::transformation::Transform& transform, f32 cameraSpeed) const;
        void RotateCamera(rei::transformation::Transform& transform) const;
        void OnUpdate() override;

    private:
        std::shared_ptr<ecs::Filter> _f;
    };
}
