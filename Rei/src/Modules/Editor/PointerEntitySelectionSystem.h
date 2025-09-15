#pragma once

namespace rei::editor
{
    class PointerEntitySelectionSystem : public ecs::System
    {
    public:
        PointerEntitySelectionSystem(const std::shared_ptr<ecs::EcsRegistry>& ecs, const std::shared_ptr<ecs::FilterProvider>& filters);
        
        void OnUpdate() override;

    private:
        std::shared_ptr<ecs::Filter> _checkEntities;
        std::shared_ptr<ecs::Filter> _selectedEntities;
        
        void ResetAllEntitiesSelection() const;
    };
}
