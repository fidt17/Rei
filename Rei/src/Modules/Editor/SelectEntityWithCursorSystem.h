#pragma once

namespace rei::editor
{
    class SelectEntityWithCursorSystem : public ecs::System
    {
    public:
        SelectEntityWithCursorSystem(const std::shared_ptr<ecs::EcsRegistry>& ecs, const std::shared_ptr<ecs::FilterProvider>& filters);
        
        void OnUpdate() override;

    private:
        std::shared_ptr<ecs::Filter> _checkEntities;
        std::shared_ptr<ecs::Filter> _selectedEntities;
        
        void ResetAllEntitiesSelection() const;
    };
}
