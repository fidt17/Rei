#pragma once
#include <map>
#include <memory>
#include <string>
#include <vector>
#include "Common/Primitives.h"

namespace rei::ecs { class World; }

namespace rei::api
{
    class SceneSyncRelay
    {
    private:
        struct EntityState
        {
            std::string Name;
            i32 ParentId;
            i32 Order;
            bool operator==(const EntityState&) const = default;
        };

        std::map<i32, EntityState> _entities;
        std::vector<i32> _selected;
        std::string _sceneAssetId;
        void (*_callback)(const char*, i32) = nullptr;
        bool _sendSnapshot = true;

    public:
        void SetCallback(void (*callback)(const char*, i32)) { _callback = callback; _sendSnapshot = true; }
        void RequestSnapshot() { _sendSnapshot = true; }
        void Capture(const std::shared_ptr<ecs::World>& world);
    };
}
