#include "pch.h"
#include "../Rei.h"
#include "EditorSelectionRelay.h"
#include "Modules/Components/EntityInfo.h"
#include "Modules/Editor/Components/SelectedTag.h"

namespace rei::api
{
    void EditorSelectionRelay::Capture(const std::shared_ptr<ecs::World>& world)
    {
        ECS_WORLD(world);
        std::vector<i32> selected;
        const auto& filter = FILTER(EntityInfo, editor::SelectedTag);
        FOR(entity, filter) selected.push_back(GET(entity, EntityInfo).Id);
        std::sort(selected.begin(), selected.end());
        if (selected == _selected) return;

        _selected = std::move(selected);
        if (_callback == nullptr) return;
        const auto json = nlohmann::json(_selected).dump();
        _callback(json.data(), static_cast<i32>(json.size()));
    }
}
