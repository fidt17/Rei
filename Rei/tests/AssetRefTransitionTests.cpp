#include "pch.h"
#include "support/AssetTestSupport.h"
#include "Modules/Assets/Core/AssetRefUtils.h"
#include "Engine/Engine.h"
#include "Api/AssetApi.h"
#include <barrier>
#include <chrono>

using namespace rei;
using namespace rei::assets;
using namespace rei::tests;

namespace
{
    struct AssignScope
    {
        AssetRef<AssetProbe>::AssignHandler Previous = AssetRef<AssetProbe>::AssignHandlerFunc;
        AssignScope() { RegisterAutoAssignHandler<AssetProbe>(); }
        ~AssignScope() { AssetRef<AssetProbe>::AssignHandlerFunc = Previous; }
    };

    void RefPack(TemporaryDirectory& files) { WriteAssetPack(files, {{"a", "first", IntegerAsset(11)}, {"b", "second", IntegerAsset(29)}}); }

    struct ConcurrentAsset
    {
        inline static std::atomic<i32> Entered = 0;
        inline static std::atomic<i32> Live = 0;
        i32 Value;
        explicit ConcurrentAsset(resources::BinaryReader& reader) : Value(reader.GetI32())
        {
            ++Live;
            ++Entered;
            // Widen the first-publication race without requiring duplicate
            // construction: a serialized implementation also completes.
            const auto deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(100);
            while (Entered < 2 && std::chrono::steady_clock::now() < deadline) std::this_thread::yield();
        }
        ~ConcurrentAsset() { --Live; }
    };
}

TEST_CASE("REF-01 Self assignment keeps sole acquisition and payload", "[native][assets][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture(RefPack);
        AssignScope assign;
        auto ref = fixture.Assets->GetById<AssetProbe>("a");
        const auto value = ref.Get();
        ref = ref;
        REQUIRE(ref.Get() == value);
        REQUIRE(probe.State->Destroyed == 0);
        fixture.Assets->Release(ref);
        REQUIRE(probe.State->Destroyed == 1);
        REQUIRE(fixture.Assets->GetLoadedAssetsSize() == 0);
    });
}

TEST_CASE("REF-01 Same ID assignment preserves loaded identity", "[native][assets][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture(RefPack);
        AssignScope assign;
        auto ref = fixture.Assets->GetById<AssetProbe>("a");
        const auto record = ref.Record;
        const auto value = ref.Get();
        ref = AssetRef<AssetProbe>("a");
        REQUIRE(ref.IsLoaded());
        CHECK(ref.Record == record);
        CHECK(ref.Get() == value);
        CHECK(probe.State->Constructed == 1);
        CHECK(probe.State->Destroyed == 0);
        fixture.Assets->Release(ref);
        CHECK(probe.State->Destroyed == 1);
    });
}

TEST_CASE("REF-01 Replacement releases A acquires B then clears B", "[native][assets][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture(RefPack);
        AssignScope assign;
        auto ref = fixture.Assets->GetById<AssetProbe>("a");
        const auto old = ref.Record;
        ref = AssetRef<AssetProbe>("b");
        REQUIRE(ref.IsLoaded());
        CHECK(ref.Get()->Value == 29);
        CHECK(old->State == AssetState::Unloaded);
        CHECK(old->Value == nullptr);
        CHECK(probe.State->Destroyed == 1);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 4);
        ref = AssetRef<AssetProbe>();
        CHECK(ref.Id.empty());
        CHECK_FALSE(ref.IsLoaded());
        CHECK(ref.Get() == nullptr);
        CHECK(probe.State->Destroyed == 2);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 0);
    });
}

TEST_CASE("REF-01 Two assigned consumers hold independent acquisitions", "[native][assets][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture(RefPack);
        AssignScope assign;
        auto first = fixture.Assets->GetById<AssetProbe>("a");
        AssetRef<AssetProbe> second;
        second = first;
        REQUIRE(first.Get() == second.Get());
        first = AssetRef<AssetProbe>();
        REQUIRE(second.IsLoaded());
        CHECK(second.Get()->Value == 11);
        CHECK(probe.State->Destroyed == 0);
        second = AssetRef<AssetProbe>();
        CHECK(probe.State->Destroyed == 1);
    });
}

TEST_CASE("REF-01 Copy constructor keeps the resource alive until the last owner is released", "[native][assets][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture(RefPack);
        AssignScope assign;
        auto original = fixture.Assets->GetById<AssetProbe>("a");
        const auto copy = original;
        REQUIRE(copy.Get() == original.Get());
        fixture.Assets->Release(original);
        CHECK(copy.IsLoaded());
        CHECK(copy.Get()->Value == 11);
        CHECK(probe.State->Destroyed == 0);
        fixture.Assets->Release(copy);
        CHECK(probe.State->Destroyed == 1);
    });
}

TEST_CASE("REF-02 External ID mutation releases bound ID and resolves new ID", "[native][assets][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture(RefPack);
        auto ref = fixture.Assets->GetById<AssetProbe>("a");
        const auto old = ref.Record;
        ref.Id = "b";
        REQUIRE_FALSE(ref.IsLoaded());
        REQUIRE(ref.Get() == nullptr);
        SyncAfterExternalChange(ref);
        REQUIRE(ref.IsLoaded());
        CHECK(ref.Get()->Value == 29);
        CHECK(old->Value == nullptr);
        CHECK(probe.State->Destroyed == 1);
        SyncAfterExternalChange(ref);
        fixture.Assets->Release(ref);
        CHECK(probe.State->Destroyed == 2);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 0);
    });
}

TEST_CASE("REF-02 External clearing releases old record without phantom load", "[native][assets][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture(RefPack);
        auto ref = fixture.Assets->GetById<AssetProbe>("a");
        ref.Id.clear();
        SyncAfterExternalChange(ref);
        CHECK(ref.Record == nullptr);
        CHECK(ref.GetBoundId().empty());
        CHECK(probe.State->Destroyed == 1);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 0);
    });
}

TEST_CASE("REF-02 Missing replacement leaves explicit unloaded view and releases A", "[native][assets][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture(RefPack);
        AssignScope assign;
        auto ref = fixture.Assets->GetById<AssetProbe>("a");
        ref = AssetRef<AssetProbe>("absent");
        CHECK(ref.Id == "absent");
        CHECK_FALSE(ref.IsLoaded());
        CHECK(ref.Get() == nullptr);
        CHECK(probe.State->Destroyed == 1);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 0);
    });
}

TEST_CASE("REF-02 Wrong typed record cannot be dereferenced through AssetRef", "[native][assets][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture;
        const auto original = fixture.Assets->CreateAssetWithId<AssetProbe>("typed", 11);
        AssetRef<UnsupportedAsset> ref("typed");
        ref.Record = original.Record;
        CHECK_FALSE(ref.IsLoaded());
        CHECK(ref.Get() == nullptr);
    });
}

TEST_CASE("ASM-02 Concurrent first publication shares one payload and balanced acquisitions", "[native][assets][coverage][coverage-remaining][isolated][stress]")
{
    Isolated([]
    {
        BehaviourFixture fixture(RefPack);
        std::barrier gate(2);
        ConcurrentAsset::Entered = 0;
        std::array<AssetRef<ConcurrentAsset>, 2> refs;
        std::array<std::exception_ptr, 2> errors;
        auto load = [&](const i32 index)
        {
            gate.arrive_and_wait();
            try { refs[index] = fixture.Assets->GetById<ConcurrentAsset>("a"); }
            catch (...) { errors[index] = std::current_exception(); }
        };
        std::thread first(load, 0), second(load, 1);
        first.join();
        second.join();
        REQUIRE(errors[0] == nullptr);
        REQUIRE(errors[1] == nullptr);
        REQUIRE(refs[0].IsLoaded());
        REQUIRE(refs[1].IsLoaded());
        CHECK(refs[0].Record == refs[1].Record);
        CHECK(refs[0].Get() == refs[1].Get());
        CHECK(ConcurrentAsset::Live == 1);
        CHECK(ConcurrentAsset::Entered == 1); // Shared bytes must be read and constructed once.
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 4);
        fixture.Assets->Release(refs[0]);
        CHECK(refs[1].IsLoaded());
        fixture.Assets->Release(refs[1]);
        CHECK(ConcurrentAsset::Live == 0);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 0);
    });
}

TEST_CASE("ABI-01 Asset API rejects null and nonpositive buffers before engine access", "[native][api][coverage][coverage-remaining]")
{
    char output[9] = "sentinel";
    CHECK(GetLoadedAssetState(nullptr, output, 8) == 0);
    CHECK(GetLoadedAssetState("id", nullptr, 8) == 0);
    CHECK(GetLoadedAssetState("id", output, 0) == 0);
    CHECK(GetLoadedAssetState("id", output, -1) == 0);
    CHECK_FALSE(GetAssetData(nullptr, "DataAsset", output, 8));
    CHECK_FALSE(GetAssetData("id", nullptr, output, 8));
    CHECK_FALSE(GetAssetData("id", "DataAsset", nullptr, 8));
    CHECK_FALSE(GetAssetData("id", "DataAsset", output, 0));
    CHECK_FALSE(GetAssetData("id", "DataAsset", output, -1));
    CHECK_FALSE(SetAssetData(nullptr, "DataAsset", "{}"));
    CHECK_FALSE(SetAssetData("id", nullptr, "{}"));
    CHECK_FALSE(SetAssetData("id", "DataAsset", nullptr));
    CHECK(std::string(output) == "sentinel");
}

TEST_CASE("ABI-01 Asset dispatch reserves terminator and preserves UTF8 byte size", "[native][api][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto asset = fixture.Assets->CreateAssetWithId<ProbeDataAsset>("utf8");
        asset.Get()->Label = "\xD0\xA0\xD0\xB5\xD0\xB9";
        const auto json = asset.Get()->REI_GET().dump();
        std::vector<char> output(json.size() + 2, 'x');
        CHECK_FALSE(DispatchTryGetAssetData("utf8", "DataAsset", output.data(), static_cast<i32>(json.size())));
        CHECK(output.front() == '\0');
        REQUIRE(DispatchTryGetAssetData("utf8", "DataAsset", output.data(), static_cast<i32>(json.size() + 1)));
        CHECK(std::string(output.data()) == json);
        CHECK(output[json.size()] == '\0');
        CHECK(output.back() == 'x');
    });
}

TEST_CASE("ABI-02 Data asset dispatch never implicitly loads and propagates malformed data", "[native][api][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture(RefPack);
        char output[128] = "guard";
        CHECK_FALSE(DispatchTryGetAssetData("a", "DataAsset", output, 128));
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
        CHECK(probe.State->Attempts == 0);
        CHECK_FALSE(DispatchTryGetAssetData("a", "Unknown", output, 128));
        const auto asset = fixture.Assets->CreateAssetWithId<AssetProbe>("loaded", 73);
        REQUIRE_THROWS(DispatchTrySetAssetData("loaded", "DataAsset", "{"));
        CHECK(asset.Get()->Value == 73);
        REQUIRE(DispatchTrySetAssetData("loaded", "DataAsset", "{\"Value\":91}"));
        CHECK(asset.Get()->Value == 91);
    });
}

TEST_CASE("REF-03 Scope exit releases AssetRef without generated code or Services", "[native][assets][regression][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture(RefPack);
        Services::GetInstance()->SetAssetManager(nullptr);
        {
            auto ref = fixture.Assets->GetById<AssetProbe>("a");
            REQUIRE(ref.IsLoaded());
            REQUIRE(fixture.Assets->Load(ref));
            REQUIRE(fixture.Assets->Load(ref));
            CHECK(probe.State->Destroyed == 0);
        }
        CHECK(probe.State->Destroyed == 1);
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 0);
    });
}

TEST_CASE("REF-03 Ordinary vector copies moves erase and clear preserve only live owners", "[native][assets][regression][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture(RefPack);
        std::vector<AssetRef<AssetProbe>> refs;
        refs.push_back(fixture.Assets->GetById<AssetProbe>("a"));
        refs.push_back(fixture.Assets->GetById<AssetProbe>("b"));
        auto copy = refs;
        refs.clear();
        REQUIRE(copy[0].IsLoaded());
        REQUIRE(copy[1].IsLoaded());
        CHECK(probe.State->Destroyed == 0);
        copy.erase(copy.begin());
        CHECK(probe.State->Destroyed == 1);
        REQUIRE(copy[0].IsLoaded());
        CHECK(copy[0].Get()->Value == 29);
        copy.clear();
        CHECK(probe.State->Destroyed == 2);
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
    });
}

TEST_CASE("REF-03 Plain ECS components release references through their normal C++ lifetime", "[native][assets][regression][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture(RefPack);
        struct PlainComponent { AssetRef<AssetProbe> Asset; };
        const auto first = fixture.Registry->NewEntity();
        const auto second = fixture.Registry->NewEntity();
        fixture.Registry->Get<PlainComponent>(first).Asset = fixture.Assets->GetById<AssetProbe>("a");
        fixture.Registry->Get<PlainComponent>(second).Asset = fixture.Assets->GetById<AssetProbe>("b");
        fixture.Registry->Del<PlainComponent>(first);
        CHECK(probe.State->Destroyed == 1);
        REQUIRE(fixture.Registry->Get<PlainComponent>(second).Asset.IsLoaded());
        fixture.Registry->Del<PlainComponent>(second);
        CHECK(probe.State->Destroyed == 2);
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
        {
            auto world = std::make_unique<ecs::World>();
            const auto entity = world->GetRegistry()->NewEntity();
            world->GetRegistry()->Get<PlainComponent>(entity).Asset = fixture.Assets->GetById<AssetProbe>("a");
            CHECK(probe.State->Destroyed == 2);
        }
        CHECK(probe.State->Destroyed == 3);
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
    });
}

TEST_CASE("REF-03 Stale owners cannot release a payload reloaded after bulk unload", "[native][assets][regression][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture(RefPack);
        auto ref = fixture.Assets->GetById<AssetProbe>("a");
        auto stale = ref;
        fixture.Assets->UnloadAllAssets();
        REQUIRE(probe.State->Destroyed == 1);
        REQUIRE(fixture.Assets->Load(ref));
        stale.Reset();
        REQUIRE(ref.IsLoaded());
        CHECK(probe.State->Destroyed == 1);
        ref.Reset();
        CHECK(probe.State->Destroyed == 2);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 0);
    });
}

TEST_CASE("REF-03 Failed PostLoad payload is released when its handle leaves scope", "[native][assets][regression][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture(RefPack);
        probe.State->ThrowPostLoad = true;
        {
            AssetRef<AssetProbe> ref("a");
            REQUIRE_FALSE(fixture.Assets->Load(ref));
            REQUIRE(ref.Record->State == AssetState::Failed);
        }
        CHECK(probe.State->Destroyed == 1);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 0);
        AssetRef<AssetProbe> cleared("a");
        REQUIRE_FALSE(fixture.Assets->Load(cleared));
        cleared.Id.clear();
        SyncAfterExternalChange(cleared);
        CHECK(cleared.Record == nullptr);
        CHECK(probe.State->Destroyed == 2);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 0);
    });
}

TEST_CASE("REF-03 Handles can outlive AssetManager without retaining its payload or calling Services", "[native][assets][regression][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture(RefPack);
        auto manager = std::make_unique<AssetManager>();
        auto ref = manager->GetById<AssetProbe>("a");
        REQUIRE(ref.IsLoaded());
        manager.reset();
        CHECK_FALSE(ref.IsLoaded());
        CHECK(probe.State->Destroyed == 1);
        ref.Reset();
        CHECK(probe.State->Destroyed == 1);
    });
}


namespace
{
    constexpr i32 DEFAULT_ASSET_BEHAVIOUR = 7105;

    void DefaultAssetPack(TemporaryDirectory& files)
    {
        WriteAssetPack(files, {
            {"default-asset", "default", JsonAsset({{"DataAssetTypeId", 7201}, {"SerializedData", SerializedField("Number", 11)}})},
            {"scene-asset", "scene", JsonAsset({{"DataAssetTypeId", 7201}, {"SerializedData", SerializedField("Number", 29)}})}
        });
    }

    nlohmann::json DefaultAssetData(const std::string& id)
    {
        const auto ref = SerializedField("Id", id);
        auto data = SerializedField("Asset", ref);
        data.update(SerializedField("Assets", nlohmann::json::array({ref})));
        const auto nested = data;
        data.update(SerializedField("Settings", nested));
        return data;
    }

    struct AssetLoadTrace
    {
        std::vector<std::string> Loads;
        std::shared_ptr<common::logging::Logger> Logger = common::logging::Log::GetLogger();
        decltype(Logger->NewLogEvent)::Handle Handle = Logger->NewLogEvent.append([this](const common::logging::LogMessage& message)
        {
            const std::string text(message.Message);
            if (text.starts_with("Loading asset ")) Loads.push_back(text);
        });

        ~AssetLoadTrace() { Logger->NewLogEvent.remove(Handle); }
        i32 Count(const std::string& id) const
        {
            return static_cast<i32>(std::ranges::count_if(Loads, [&](const auto& text) { return text.ends_with("id=" + id); }));
        }
    };

    void CheckDefaultAssetRefs(ProbeWithDefaultAssets& behaviour, const i32 number)
    {
        REQUIRE(behaviour.Asset.IsLoaded());
        CHECK(behaviour.Asset.Get()->Number == number);
        REQUIRE(behaviour.Assets.size() == 1);
        CHECK(behaviour.Assets.front().Get() == behaviour.Asset.Get());
        CHECK(behaviour.Settings.Asset.Get() == behaviour.Asset.Get());
        REQUIRE(behaviour.Settings.Assets.size() == 1);
        CHECK(behaviour.Settings.Assets.front().Get() == behaviour.Asset.Get());
    }
}

TEST_CASE("REF-04 Component creation loads serialized assets without reading discarded defaults", "[native][assets][behaviour][regression][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture(DefaultAssetPack);
        AssetLoadTrace loads;
        for (i32 id = 1; id <= 3; ++id)
        {
            fixture.Manager->Create(SceneEntity(SceneObject(id, "serialized", nlohmann::json::array({
                SceneBehaviour(DEFAULT_ASSET_BEHAVIOUR, DefaultAssetData("scene-asset"))
            }))));
            fixture.World->RefreshAll();
            auto& behaviour = fixture.Registry->Get<ProbeWithDefaultAssets>(fixture.Manager->GetBySceneId(id));
            CheckDefaultAssetRefs(behaviour, 29);
            CHECK(behaviour.GetBehaviourId() == DEFAULT_ASSET_BEHAVIOUR);
            CHECK(behaviour.GetEntity() == fixture.Manager->GetBySceneId(id));
        }
        CHECK(loads.Count("default-asset") == 0);
        CHECK(loads.Count("scene-asset") == 1);
        for (const auto entity : LiveSceneEntities(fixture))
        {
            fixture.Registry->DestroyEntity(entity);
        }
        fixture.World->Refresh();
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
    });
}

TEST_CASE("REF-04 Missing serialized fields load scalar collection and nested defaults before Init", "[native][assets][behaviour][regression][isolated]")
{
    Isolated([]
    {
        for (const auto& data : {nlohmann::json(), nlohmann::json::object(), SerializedField("Unknown", 7)})
        {
            BehaviourFixture fixture(DefaultAssetPack);
            AssetLoadTrace loads;
            const auto entity = fixture.Entity();
            bool initialized = false;
            fixture.Events->Action = [&](const i32 id, const std::string& stage, const ecs::Entity current)
            {
                if (id != DEFAULT_ASSET_BEHAVIOUR || stage != "Init") return;
                initialized = true;
                CheckDefaultAssetRefs(fixture.Registry->Get<ProbeWithDefaultAssets>(current), 11);
            };
            fixture.Manager->AddBehaviour(entity, DEFAULT_ASSET_BEHAVIOUR, data);
            CHECK(initialized);
            CHECK(loads.Count("default-asset") == 1);
            CHECK(loads.Count("scene-asset") == 0);
            fixture.Manager->GetBehaviourRegistry().DeleteBehaviour(entity, DEFAULT_ASSET_BEHAVIOUR);
            CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
        }
    });
}

TEST_CASE("REF-04 Explicit empty and missing asset IDs never fall back to defaults", "[native][assets][behaviour][regression][isolated]")
{
    Isolated([]
    {
        for (const std::string id : {"", "missing-asset"})
        {
            BehaviourFixture fixture(DefaultAssetPack);
            AssetLoadTrace loads;
            const auto entity = fixture.Entity();
            fixture.Manager->AddBehaviour(entity, DEFAULT_ASSET_BEHAVIOUR, DefaultAssetData(id));
            const auto& behaviour = fixture.Registry->Get<ProbeWithDefaultAssets>(entity);
            CHECK(behaviour.Asset.Id == id);
            CHECK_FALSE(behaviour.Asset.IsLoaded());
            CHECK_FALSE(behaviour.Assets.front().IsLoaded());
            CHECK_FALSE(behaviour.Settings.Asset.IsLoaded());
            CHECK_FALSE(behaviour.Settings.Assets.front().IsLoaded());
            CHECK(loads.Count("default-asset") == 0);
            CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
            fixture.Manager->GetBehaviourRegistry().DeleteBehaviour(entity, DEFAULT_ASSET_BEHAVIOUR);
        }
    });
}

TEST_CASE("REF-04 Required component waits for its scene data before loading defaults", "[native][assets][behaviour][regression][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture(DefaultAssetPack);
        AssetLoadTrace loads;
        fixture.Manager->GetBehaviourRegistry().RegisterComponent<ProbeC>(PROBE_C,
            [](ecs::Entity) { return nlohmann::json(); }, [](ecs::Entity, const nlohmann::json&) {}, nullptr, {DEFAULT_ASSET_BEHAVIOUR});
        fixture.Manager->Create(SceneEntity(SceneObject(1, "required", nlohmann::json::array({
            SceneBehaviour(PROBE_C), SceneBehaviour(DEFAULT_ASSET_BEHAVIOUR, DefaultAssetData("scene-asset"))
        }))));
        fixture.World->RefreshAll();
        const auto entity = fixture.Manager->GetBySceneId(1);
        CheckDefaultAssetRefs(fixture.Registry->Get<ProbeWithDefaultAssets>(entity), 29);
        CHECK(loads.Count("default-asset") == 0);
        CHECK(loads.Count("scene-asset") == 1);
        for (const auto entity : LiveSceneEntities(fixture))
        {
            fixture.Registry->DestroyEntity(entity);
        }
        fixture.World->Refresh();
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
    });
}

TEST_CASE("REF-04 Cloning shares final assets and releases them after the last component", "[native][assets][behaviour][regression][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture(DefaultAssetPack);
        AssetLoadTrace loads;
        const auto source = fixture.Manager->CreateNewEntity("source");
        fixture.Manager->AddBehaviour(source, DEFAULT_ASSET_BEHAVIOUR, DefaultAssetData("scene-asset"));
        const auto clone = fixture.Manager->Instantiate(source, "clone", false);
        CheckDefaultAssetRefs(fixture.Registry->Get<ProbeWithDefaultAssets>(clone), 29);
        CHECK(loads.Count("default-asset") == 0);
        CHECK(loads.Count("scene-asset") == 1);
        fixture.Manager->GetBehaviourRegistry().DeleteBehaviour(source, DEFAULT_ASSET_BEHAVIOUR);
        CheckDefaultAssetRefs(fixture.Registry->Get<ProbeWithDefaultAssets>(clone), 29);
        fixture.Manager->GetBehaviourRegistry().DeleteBehaviour(clone, DEFAULT_ASSET_BEHAVIOUR);
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
    });
}
