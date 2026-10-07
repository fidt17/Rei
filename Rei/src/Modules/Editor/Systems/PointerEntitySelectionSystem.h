#pragma once
#include "Ecs/System.h"
#include <unordered_set>

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
        std::unordered_set<ecs::Entity> _pressHits;
        std::unordered_set<ecs::Entity> _pickHistory;
        u64 _sourceRevision = 0;
        bool _restartCycle = false;
        
        void ResetAllEntitiesSelection() const;
        ecs::Entity FindSelectionCandidate();
        void ResetPickCycle();
        void RememberPick(ecs::Entity entity);
        void CommitSelection(ecs::Entity selectedCandidate, bool additiveSelection) const;
        bool IsCandidateValid(ecs::Entity candidate) const;
        bool IsSelectionBlocked() const;
    };
}
