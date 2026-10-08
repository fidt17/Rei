#include "pch.h"
#include "../Rei.h"
#include "SceneSyncRelay.h"
#include "Modules/Components/EntityInfo.h"
#include "Modules/Editor/Components/SelectedTag.h"
#include "rei_behaviours/transformation/Transform.h"

namespace rei::api
{
    void SceneSyncRelay::Capture(const std::shared_ptr<ecs::World>& world)
    {
        if (_callback == nullptr) return;
        ECS_WORLD(world);
        std::map<i32, EntityState> entities;
        std::vector<i32> selected;
        const auto& filter = FILTER(EntityInfo);
        FOR(entity, filter)
        {
            const auto& info = GET(entity, EntityInfo);
            i32 parentId = 0;
            i32 order = 0;
            if (HAS(entity, Transform))
            {
                const auto& transform = GET(entity, Transform);
                const auto parent = transform.GetParent();
                if (!IS_DEAD(parent) && HAS(parent, EntityInfo)) parentId = GET(parent, EntityInfo).Id;
                order = transform.GetChildOrder();
            }
            entities.emplace(info.Id, EntityState{info.Name, parentId, order});
            if (HAS(entity, editor::SelectedTag)) selected.push_back(info.Id);
        }
        std::sort(selected.begin(), selected.end());
        if (!_sendSnapshot && entities == _entities && selected == _selected) return;

        nlohmann::json changed = nlohmann::json::array();
        for (const auto& [id, state] : entities)
        {
            const auto previous = _entities.find(id);
            if (!_sendSnapshot && previous != _entities.end() && previous->second == state) continue;
            changed.push_back({{"Id", id}, {"Name", state.Name}, {"ParentId", state.ParentId}, {"Order", state.Order}});
        }
        std::vector<i32> removed;
        if (!_sendSnapshot)
        {
            for (const auto& [id, state] : _entities)
            {
                if (!entities.contains(id)) removed.push_back(id);
            }
        }
        const nlohmann::json packet = {
            {"IsSnapshot", _sendSnapshot}, {"Entities", changed},
            {"RemovedEntityIds", removed}, {"SelectedEntityIds", selected}
        };
        _entities = std::move(entities);
        _selected = std::move(selected);
        _sendSnapshot = false;
        const auto json = packet.dump();
        _callback(json.data(), static_cast<i32>(json.size()));
    }
}
