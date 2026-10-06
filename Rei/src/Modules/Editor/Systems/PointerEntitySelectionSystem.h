#pragma once
#include "Ecs/System.h"

namespace rei::editor
{
    class PointerEntitySelectionSystem : public ecs::System
    {
    public:
        PointerEntitySelectionSystem(const std::shared_ptr<ecs::World>& world);
        
        void OnUpdate() override;

    private:
        std::shared_ptr<ecs::Filter> _checkEntities;
        std::shared_ptr<ecs::Filter> _blockSelectionEntities;
        ecs::Entity _candidateCamera = ecs::NULL_ENTITY;
        
        void ResetAllEntitiesSelection() const;
        ecs::Entity FindSelectionCandidate() const;
        void CommitSelection(ecs::Entity selectedCandidate, bool additiveSelection) const;
        bool IsCandidateValid(ecs::Entity candidate) const;
        bool IsSelectionBlocked() const;
    };
}
