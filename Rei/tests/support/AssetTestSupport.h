#pragma once
#include "BehaviourTestFixture.h"
#include "Modules/Scenes/SceneAssetPreloader.h"
#include <mutex>
#include <thread>

namespace rei::tests
{
    // Independently encode Windows little-endian fixtures, without BinaryWriter.
    inline void AppendInteger(std::vector<u8>& bytes, const u64 value, const u32 width)
    {
        for (u32 i = 0; i < width; ++i) bytes.push_back(static_cast<u8>(value >> (8 * i)));
    }

    inline void AppendString(std::vector<u8>& bytes, const std::string& value)
    {
        AppendInteger(bytes, value.size(), 4);
        bytes.insert(bytes.end(), value.begin(), value.end());
    }

    struct PackedAsset
    {
        std::string Id;
        std::string Name;
        std::vector<u8> Bytes;
    };

    inline std::vector<u8> IntegerAsset(const i32 value)
    {
        std::vector<u8> bytes;
        AppendInteger(bytes, static_cast<u32>(value), 4);
        return bytes;
    }

    inline std::vector<u8> JsonAsset(const nlohmann::json& value)
    {
        std::vector<u8> bytes;
        AppendString(bytes, value.dump());
        return bytes;
    }

    inline void WriteAssetPack(TemporaryDirectory& files, const std::vector<PackedAsset>& assets)
    {
        std::vector<u8> pack = {0xcc, 0x55, 0xaa}; // Nonzero, deliberately unaligned first offset.
        std::vector<u8> map;
        AppendInteger(map, assets.size(), 4);
        for (const auto& asset : assets)
        {
            AppendString(map, asset.Id);
            AppendString(map, asset.Name);
            AppendString(map, "pack.bin");
            AppendInteger(map, pack.size(), 8);
            pack.insert(pack.end(), asset.Bytes.begin(), asset.Bytes.end());
        }
        files.Write("pack.bin", pack);
        files.Write("map.bin", map);
    }

    struct AssetProbeState
    {
        std::atomic<i32> Attempts = 0;
        std::atomic<i32> Constructed = 0;
        std::atomic<i32> Destroyed = 0;
        std::atomic<i32> PostLoads = 0;
        std::atomic<i32> Resolves = 0;
        bool ThrowPostLoad = false;
        bool ThrowGet = false;
        std::mutex Mutex;
        std::vector<std::thread::id> PostLoadThreads;
        std::function<void()> OnPostLoad;
    };

    inline std::shared_ptr<AssetProbeState> CurrentAssetProbe;

    class AssetProbeScope
    {
    public:
        std::shared_ptr<AssetProbeState> State = std::make_shared<AssetProbeState>();
        AssetProbeScope() : _previous(CurrentAssetProbe) { CurrentAssetProbe = State; }
        ~AssetProbeScope() { CurrentAssetProbe = _previous; }
    private:
        std::shared_ptr<AssetProbeState> _previous;
    };

    struct AssetProbe
    {
        i32 Value;
        std::shared_ptr<AssetProbeState> State;
        explicit AssetProbe(const i32 value = 17) : Value(value), State(CurrentAssetProbe)
        {
            if (!State) throw std::logic_error("AssetProbeScope is required");
            ++State->Attempts;
            if (Value == -99) throw std::runtime_error("probe constructor failure");
            ++State->Constructed;
        }
        explicit AssetProbe(resources::BinaryReader& reader) : AssetProbe(reader.GetI32()) {}
        ~AssetProbe() { ++State->Destroyed; }
        void PostLoad()
        {
            ++State->PostLoads;
            {
                std::scoped_lock lock(State->Mutex);
                State->PostLoadThreads.push_back(std::this_thread::get_id());
            }
            if (State->ThrowPostLoad) throw std::runtime_error("probe post-load failure");
            if (State->OnPostLoad) State->OnPostLoad();
        }
        nlohmann::json REI_GET() const
        {
            if (State->ThrowGet) throw std::runtime_error("probe read failure");
            return {{"Value", Value}};
        }
        void REI_SET(const nlohmann::json& data) { Value = data.at("Value").get<i32>(); }
        void ResolveDependencies() { ++State->Resolves; }
    };

    struct UnsupportedAsset
    {
        explicit UnsupportedAsset(i32 = 0) {}
        explicit UnsupportedAsset(resources::BinaryReader&) {}
    };

    inline nlohmann::json SerializedField(const std::string& name, nlohmann::json value) { return {{name, {{"Value", std::move(value)}}}}; }
    inline nlohmann::json SceneBehaviour(const i32 id, nlohmann::json data = nlohmann::json::object()) { return {{"Id", id}, {"SerializedData", std::move(data)}}; }
    inline nlohmann::json SceneObject(const i32 id, const std::string& name, nlohmann::json behaviours) { return {{"Id", id}, {"Name", name}, {"Behaviours", std::move(behaviours)}}; }

    inline std::vector<ecs::Entity> LiveSceneEntities(BehaviourFixture& fixture)
    {
        fixture.World->RefreshAll();
        std::vector<ecs::Entity> result;
        for (const auto entity : fixture.World->GetFiltersRegistry()->Get<EntityInfo>()->Entities())
        {
            if (fixture.Registry->IsAlive(entity)) result.push_back(entity);
        }
        return result;
    }
}
