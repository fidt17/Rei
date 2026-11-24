#include "pch.h"
#include "support/BehaviourTestFixture.h"
#include "support/GeometryTestSupport.h"
#include "Modules/Resources/Serialization/BinaryWriter.h"
#include "Modules/Scenes/SceneAssetPreloader.h"
#include <limits>

// Produced at Tests build time by production BehaviourRegistrySourceGenerator.
// Keep template implementations in this translation unit for typed ref probes.
#include "BehaviourRegistry.cpp"

using namespace rei;
using namespace rei::tests;
using nlohmann::json;

namespace
{
    json Field(const std::string& name, json value) { return {{name, {{"Value", std::move(value)}}}}; }

    void SetAssetCollection(BehaviourFixture& fixture, const ecs::Entity entity, const std::vector<std::string>& ids)
    {
        auto items = json::array();
        for (const auto& id : ids) items.push_back(Field("Id", id));
        fixture.Manager->GetBehaviourRegistry().SetBehaviourData(entity, PROBE_A, Field("Assets", items));
    }

    std::filesystem::path Package(BehaviourFixture& fixture, const std::string& body)
    {
        const auto path = fixture.Files.Write("asset.bin", {});
        resources::BinaryWriter writer(path.string(), 0);
        writer.WriteStr(body);
        writer.Close();
        return path;
    }
}

TEST_CASE("SER-03 Removing packed components releases their AssetRef fields without changing surviving owners", "[native][serialization][generated][regression][isolated]")
{
    const auto firstRemoved = GENERATE(0, 1, 2);
    Isolated([firstRemoved]
    {
        BehaviourFixture fixture;
        const auto shared = fixture.Assets->CreateAssetWithId<ProbeDataAsset>("shared");
        std::array<ecs::Entity, 3> entities{ecs::NULL_ENTITY, ecs::NULL_ENTITY, ecs::NULL_ENTITY};
        std::array<assets::AssetRef<ProbeDataAsset>, 3> views;
        for (i32 i = 0; i < 3; ++i)
        {
            entities[i] = fixture.Entity(100 + i);
            fixture.Add(entities[i], PROBE_A, false);
            auto own = fixture.Assets->CreateAssetWithId<ProbeDataAsset>("owner-" + STRING(i));
            views[i].Id = own.Id;
            views[i].Record = own.Record;
            fixture.Manager->GetBehaviourRegistry().SetBehaviourData(entities[i], PROBE_A, Field("Asset", Field("Id", own.Id)));
            SetAssetCollection(fixture, entities[i], {shared.Id, shared.Id});
            fixture.Assets->Release(own);
        }
        fixture.Assets->Release(shared);
        REQUIRE(fixture.Assets->GetLoadedAssetCount() == 4);

        fixture.Registry->Del<ProbeA>(entities[firstRemoved]);
        CHECK_FALSE(views[firstRemoved].IsLoaded());
        CHECK(shared.IsLoaded());
        for (i32 i = 0; i < 3; ++i)
        {
            if (i == firstRemoved) continue;
            REQUIRE(views[i].IsLoaded());
            REQUIRE(fixture.Registry->Get<ProbeA>(entities[i]).Asset.Get() == views[i].Get());
            fixture.Registry->Del<ProbeA>(entities[i]);
            CHECK_FALSE(views[i].IsLoaded());
        }
        CHECK_FALSE(shared.IsLoaded());
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
    });
}

TEST_CASE("SER-03 Releasing an asset releases its nested AssetRefs and preserves external owners", "[native][serialization][generated][regression][isolated]")
{
    const auto keepExternalOwner = GENERATE(false, true);
    Isolated([keepExternalOwner]
    {
        BehaviourFixture fixture;
        auto child = fixture.Assets->CreateAssetWithId<ProbeDataAsset>("child");
        auto retained = keepExternalOwner ? fixture.Assets->GetById<ProbeDataAsset>(child.Id) : assets::AssetRef<ProbeDataAsset>();
        auto owner = fixture.Assets->CreateAssetWithId<ProbeAssetOwner>("owner");
        owner->Asset = child;
        owner->Assets.emplace_back(child.Id);
        owner->Assets.emplace_back(child.Id);
        owner->ResolveDependencies();
        fixture.Assets->Release(child);
        REQUIRE(child.IsLoaded());
        fixture.Assets->Release(owner);
        CHECK_FALSE(owner.IsLoaded());
        CHECK(child.IsLoaded() == keepExternalOwner);
        if (keepExternalOwner) fixture.Assets->Release(retained);
        CHECK_FALSE(child.IsLoaded());
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
    });
}

TEST_CASE("SER-01 Generated getter exposes independent native scalar and nested values", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        auto& probe = fixture.Registry->Get<ProbeA>(entity);
        probe.Number = -17;
        probe.Ratio = 3.5f;
        probe.Flag = false;
        probe.Text = "Unicode \xD0\xA0\xD0\xB5\xD0\xB9";
        probe.Mode = ProbeMode::Hot;
        probe.Settings.Value = 29;
        probe.Settings.Label = "custom";
        probe.Values = {9, -4, 2};
        const auto data = fixture.Manager->GetBehaviourRegistry().GetBehaviourData(entity, PROBE_A);
        CHECK(data.at("REI_TYPE") == "ProbeA");
        CHECK(data.at("Number") == -17);
        CHECK(data.at("Ratio") == 3.5);
        CHECK(data.at("Flag") == false);
        CHECK(data.at("Text") == "Unicode \xD0\xA0\xD0\xB5\xD0\xB9");
        CHECK(data.at("Mode") == 5);
        CHECK(data.at("Settings") == json{{"REI_TYPE", "ProbeNested"}, {"Value", 29}, {"Label", "custom"}});
        CHECK(data.at("Values") == json::array({9, -4, 2}));
        CHECK(fixture.Events->Calls == std::vector<std::string>{"7101:BeforeGet"});
    });
}

TEST_CASE("SER-01 Generated setter applies wrappers to actual native fields", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        const json data = {{"Number", {{"Value", 123}}}, {"Ratio", {{"Value", 2.5}}}, {"Flag", {{"Value", false}}}, {"Text", {{"Value", "updated"}}}, {"Mode", {{"Value", 5}}}};
        fixture.Manager->GetBehaviourRegistry().SetBehaviourData(entity, PROBE_A, data);
        const auto& probe = fixture.Registry->Get<ProbeA>(entity);
        CHECK(probe.Number == 123);
        CHECK(probe.Ratio == Catch::Approx(2.5f));
        CHECK_FALSE(probe.Flag);
        CHECK(probe.Text == "updated");
        CHECK(probe.Mode == ProbeMode::Hot);
        CHECK(fixture.Events->Calls == std::vector<std::string>{"7101:AfterSet"});
    });
}

TEST_CASE("SER-01 Sparse setter preserves omitted fields and ignores unknown keys", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        auto& probe = fixture.Registry->Get<ProbeA>(entity);
        probe.Number = 99;
        probe.Values = {8, 9};
        probe.REI_SET(json{{"Unknown", {{"Value", 10}}}, {"Settings", {{"Value", Field("Value", 123)}}}});
        CHECK(probe.Number == 99);
        CHECK(probe.Text == "seed");
        CHECK(probe.Values == std::vector<i32>{8, 9});
        CHECK(probe.Settings.Value == 123);
        CHECK(probe.Settings.Label == "nested");
        probe.REI_SET(json::object());
        CHECK(probe.Settings.Value == 123);
    });
}

TEST_CASE("SER-01 Generated primitive and enum collections replace and clear", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        auto& probe = fixture.Registry->Get<ProbeA>(entity);
        probe.REI_SET(json{{"Values", {{"Value", json::array({4, 5, -8})}}}, {"Modes", {{"Value", json::array({0, 5, 2})}}}});
        CHECK(probe.Values == std::vector<i32>{4, 5, -8});
        CHECK(probe.Modes == std::vector<ProbeMode>{ProbeMode::Cold, ProbeMode::Hot, ProbeMode::Warm});
        CHECK(probe.REI_GET().at("Modes") == json::array({0, 5, 2}));
        probe.REI_SET(Field("Values", json::array()));
        CHECK(probe.Values.empty());
        CHECK(probe.Modes.size() == 3);
    });
}

TEST_CASE("SER-01 Generated custom collection has native values and flat getter items", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        auto& probe = fixture.Registry->Get<ProbeA>(entity);
        probe.REI_SET(Field("Items", json::array({Field("Value", 31), Field("Label", "second")})));
        REQUIRE(probe.Items.size() == 2);
        CHECK(probe.Items[0].Value == 31);
        CHECK(probe.Items[0].Label == "nested");
        CHECK(probe.Items[1].Value == 11);
        CHECK(probe.Items[1].Label == "second");
        CHECK(probe.REI_GET().at("Items")[0] == json{{"REI_TYPE", "ProbeNested"}, {"Value", 31}, {"Label", "nested"}});
        probe.REI_SET(Field("Items", json::array()));
        CHECK(probe.Items.empty());
    });
}

TEST_CASE("SER-01 Generated ComponentRefs resolve native scene IDs and missing targets", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto owner = fixture.Entity(42);
        const auto target = fixture.Entity(77);
        fixture.Registry->Get<Transform>(target).GetLocalPosition() = {7, 8, 9};
        fixture.Add(owner, PROBE_A, false);
        const json data = {{"Target", {{"Value", Field("SceneEntityId", 77)}}}, {"Targets", {{"Value", json::array({Field("SceneEntityId", 77), Field("SceneEntityId", 999), Field("SceneEntityId", 0)})}}}};
        fixture.Manager->GetBehaviourRegistry().SetBehaviourData(owner, PROBE_A, data);
        const auto& probe = fixture.Registry->Get<ProbeA>(owner);
        REQUIRE_FALSE(probe.Target.IsNull());
        CHECK(&probe.Target.Get() == &fixture.Registry->Get<Transform>(target));
        CheckVector(probe.Target.Get().GetLocalPosition(), {7, 8, 9});
        REQUIRE(probe.Targets.size() == 3);
        CHECK_FALSE(probe.Targets[0].IsNull());
        CHECK(probe.Targets[1].IsNull());
        CHECK(probe.Targets[2].IsNull());
        CHECK(probe.REI_GET().at("Targets")[1].at("SceneEntityId") == 999);
    });
}

TEST_CASE("SER-01 Generated AssetRef binds actual manager payload and clears ownership", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        auto created = fixture.Assets->CreateAssetWithId<ProbeDataAsset>("data-a");
        created.Get()->Number = 37;
        fixture.Manager->GetBehaviourRegistry().SetBehaviourData(entity, PROBE_A, Field("Asset", Field("Id", "data-a")));
        auto& probe = fixture.Registry->Get<ProbeA>(entity);
        REQUIRE(probe.Asset.IsLoaded());
        CHECK(probe.Asset.Get() == created.Get());
        CHECK(probe.Asset.Get()->Number == 37);
        fixture.Assets->Release(created);
        CHECK(probe.Asset.IsLoaded());
        fixture.Manager->GetBehaviourRegistry().SetBehaviourData(entity, PROBE_A, Field("Asset", Field("Id", "")));
        CHECK_FALSE(probe.Asset.IsLoaded());
        CHECK_FALSE(created.IsLoaded());
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
    });
}

TEST_CASE("SER-01 Clearing generated AssetRef collection releases its acquisitions", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        const i32 count = GENERATE(1, 3);
        const bool keepExternalOwner = GENERATE(false, true);
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        auto created = fixture.Assets->CreateAssetWithId<ProbeDataAsset>("data-a");
        const std::weak_ptr<void> payload = created.Record->Value;
        SetAssetCollection(fixture, entity, std::vector<std::string>(count, "data-a"));
        auto& probe = fixture.Registry->Get<ProbeA>(entity);
        REQUIRE(probe.Assets.size() == count);
        for (const auto& ref : probe.Assets) REQUIRE(ref.Get() == created.Get());
        if (!keepExternalOwner) fixture.Assets->Release(created);
        SetAssetCollection(fixture, entity, {});
        CHECK(probe.Assets.empty());
        CHECK(created.IsLoaded() == keepExternalOwner);
        CHECK(payload.expired() == !keepExternalOwner);
        SetAssetCollection(fixture, entity, {});
        CHECK(created.IsLoaded() == keepExternalOwner);
        if (keepExternalOwner) fixture.Assets->Release(created);
        CHECK(payload.expired());
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
    });
}

TEST_CASE("SER-03 Replacing AssetRef collection balances duplicate and retained runtime resources", "[native][serialization][generated][regression][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        auto first = fixture.Assets->CreateAssetWithId<ProbeDataAsset>("first");
        auto removed = fixture.Assets->CreateAssetWithId<ProbeDataAsset>("removed");
        auto added = fixture.Assets->CreateAssetWithId<ProbeDataAsset>("added");
        first.Get()->Number = 31;
        added.Get()->Number = 59;
        const auto firstPayload = first.Get();
        const std::weak_ptr<void> firstLifetime = first.Record->Value;
        const std::weak_ptr<void> removedLifetime = removed.Record->Value;
        const std::weak_ptr<void> addedLifetime = added.Record->Value;
        SetAssetCollection(fixture, entity, {"first", "first", "removed"});
        fixture.Assets->Release(first);
        fixture.Assets->Release(removed);
        SetAssetCollection(fixture, entity, {"first", "added"});
        fixture.Assets->Release(added);
        for (i32 i = 0; i < 3; ++i)
        {
            SetAssetCollection(fixture, entity, {"first", "added"});
            const auto& refs = fixture.Registry->Get<ProbeA>(entity).Assets;
            REQUIRE(refs.size() == 2);
            REQUIRE(refs[0].Get() == firstPayload);
            CHECK(refs[0].Get()->Number == 31);
            REQUIRE(refs[1].IsLoaded());
            CHECK(refs[1].Get()->Number == 59);
            CHECK(removedLifetime.expired());
        }
        SetAssetCollection(fixture, entity, {});
        CHECK(firstLifetime.expired());
        CHECK(addedLifetime.expired());
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
    });
}

TEST_CASE("SER-03 Clearing AssetRef collection releases bound IDs and skips unbound references", "[native][serialization][generated][regression][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        auto bound = fixture.Assets->CreateAssetWithId<ProbeDataAsset>("bound");
        const std::weak_ptr<void> lifetime = bound.Record->Value;
        SetAssetCollection(fixture, entity, {"late", "", "bound"});
        fixture.Assets->Release(bound);
        auto late = fixture.Assets->CreateAssetWithId<ProbeDataAsset>("late");
        auto& refs = fixture.Registry->Get<ProbeA>(entity).Assets;
        REQUIRE(refs.size() == 3);
        REQUIRE_FALSE(refs[0].IsLoaded());
        refs[2].Id = "late"; // The acquisition still belongs to the bound record.
        SetAssetCollection(fixture, entity, {});
        CHECK(lifetime.expired());
        CHECK(late.IsLoaded());
        CHECK(fixture.Assets->GetLoadedAssetCount() == 1);
        fixture.Assets->Release(late);
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
    });
}

TEST_CASE("SER-03 Clearing AssetRef collection releases failed payloads", "[native][serialization][generated][regression][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        auto created = fixture.Assets->CreateAssetWithId<ProbeDataAsset>("failed");
        const std::weak_ptr<void> payload = created.Record->Value;
        SetAssetCollection(fixture, entity, {"failed"});
        fixture.Assets->Release(created);
        created.Record->State = assets::AssetState::Failed;
        SetAssetCollection(fixture, entity, {});
        CHECK(payload.expired());
        CHECK(created.Record->State == assets::AssetState::Unloaded);
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
    });
}

TEST_CASE("SER-03 Malformed AssetRef collection preserves current acquisitions without leaks", "[native][serialization][generated][regression][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        auto retained = fixture.Assets->CreateAssetWithId<ProbeDataAsset>("retained");
        auto candidate = fixture.Assets->CreateAssetWithId<ProbeDataAsset>("candidate");
        const std::weak_ptr<void> retainedLifetime = retained.Record->Value;
        const std::weak_ptr<void> candidateLifetime = candidate.Record->Value;
        SetAssetCollection(fixture, entity, {"retained"});
        fixture.Assets->Release(retained);
        const auto original = fixture.Registry->Get<ProbeA>(entity).REI_GET().at("Assets");
        const std::vector<json> invalid = {42, json::object(), json::array({Field("Id", "candidate"), Field("Id", 42)}),
            json::array({Field("Id", "candidate"), json{{"Id", "missing wrapper"}}})};
        for (const auto& data : invalid)
        {
            CHECK_THROWS(fixture.Manager->GetBehaviourRegistry().SetBehaviourData(entity, PROBE_A, Field("Assets", data)));
            CHECK(fixture.Registry->Get<ProbeA>(entity).REI_GET().at("Assets") == original);
            CHECK_FALSE(retainedLifetime.expired());
        }
        fixture.Manager->GetBehaviourRegistry().SetBehaviourData(entity, PROBE_A, Field("Number", 17));
        CHECK(fixture.Registry->Get<ProbeA>(entity).REI_GET().at("Assets") == original);
        fixture.Assets->Release(candidate);
        CHECK(candidateLifetime.expired());
        SetAssetCollection(fixture, entity, {});
        CHECK(retainedLifetime.expired());
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
    });
}

TEST_CASE("SER-01 Generated dependency collector accepts flat and wrapped asset IDs", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        auto& registry = fixture.Manager->GetBehaviourRegistry();
        const json flat = {{"Asset", {{"Id", "one"}}}, {"Assets", json::array({json{{"Id", "two"}}, json{{"Id", ""}}, json{{"Id", 9}}})}};
        json wrapped = Field("Asset", Field("Id", "one"));
        wrapped.update(Field("Assets", json::array({Field("Id", "two"), Field("Id", ""), Field("Id", 9)})));
        for (const auto& data : {flat, wrapped})
        {
            std::vector<assets::AssetDependency> dependencies;
            registry.CollectAssetDependencies(PROBE_A, data, dependencies);
            REQUIRE(dependencies.size() == 2);
            CHECK(dependencies[0].Id == "one");
            CHECK(dependencies[1].Id == "two");
            CHECK(fixture.Assets->GetLoadedAssetCount() == 0); // Collection is a read, without loading.
        }
    });
}

TEST_CASE("SER-01 Generated typed dependency loads correct native asset without acquisition", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        auto ref = fixture.Assets->CreateAssetWithId<ProbeDataAsset>("typed");
        ref.Get()->Number = 59;
        std::vector<assets::AssetDependency> dependencies;
        fixture.Manager->GetBehaviourRegistry().CollectAssetDependencies(PROBE_A, json{{"Asset", {{"Id", "typed"}}}}, dependencies);
        REQUIRE(dependencies.size() == 1);
        const scenes::SceneAssetPreloader preloader(fixture.Assets);
        REQUIRE(dependencies[0].LoadData(preloader));
        CHECK(ref.Get()->Number == 59);
        fixture.Assets->Release(ref);
        CHECK_FALSE(ref.IsLoaded());
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
    });
}

TEST_CASE("SER-02 Missing wrapper and wrong scalar type fail before field mutation", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        auto& probe = fixture.Registry->Get<ProbeA>(entity);
        CHECK_THROWS(probe.REI_SET(json{{"Number", 5}}));
        CHECK_THROWS(probe.REI_SET(Field("Number", "wrong")));
        CHECK_THROWS(probe.REI_SET(Field("Flag", 1)));
        CHECK(probe.Number == 7);
        CHECK(probe.Flag);
        CHECK(fixture.Events->Calls.empty());
    });
}

TEST_CASE("SER-02 Integer overflow rejects without silent narrowing", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        auto& probe = fixture.Registry->Get<ProbeA>(entity);
        const auto min = std::numeric_limits<i32>::min();
        const auto max = std::numeric_limits<i32>::max();
        for (const auto& invalid : {json(static_cast<i64>(min) - 1), json(static_cast<i64>(max) + 1), json(std::numeric_limits<u64>::max())})
        {
            CHECK_THROWS(probe.REI_SET(Field("Number", invalid)));
            CHECK(probe.Number == 7);
        }

        probe.REI_SET(Field("Number", min));
        CHECK(probe.Number == min);
        probe.REI_SET(Field("Number", static_cast<u64>(max)));
        CHECK(probe.Number == max);
    });
}

TEST_CASE("SER-02 Fractional input rejects for native integer field", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        auto& probe = fixture.Registry->Get<ProbeA>(entity);
        CHECK_THROWS(probe.REI_SET(Field("Number", 1.5)));
        CHECK(probe.Number == 7);
    });
}

TEST_CASE("SER-02 Later field failure leaves earlier native write applied", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        const json data = {{"Number", {{"Value", 99}}}, {"Ratio", {{"Value", "invalid"}}}};
        CHECK_THROWS(fixture.Manager->GetBehaviourRegistry().SetBehaviourData(entity, PROBE_A, data));
        const auto& probe = fixture.Registry->Get<ProbeA>(entity);
        CHECK(probe.Number == 99); // Characterization: no atomic setter guarantee claimed.
        CHECK(probe.Ratio == Catch::Approx(1.25f));
        CHECK(fixture.Count(PROBE_A, "AfterSet") == 0);
    });
}

TEST_CASE("SER-02 Invalid primitive collection throws after clearing prior contents", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        auto& probe = fixture.Registry->Get<ProbeA>(entity);
        CHECK_THROWS(probe.REI_SET(Field("Values", json::array({1, "wrong"}))));
        CHECK(probe.Values.empty()); // Characterization of observable partial mutation.
    });
}

TEST_CASE("SER-02 Custom collection rejects nonarray body", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        CHECK_THROWS(fixture.Registry->Get<ProbeA>(entity).REI_SET(Field("Items", 42)));
    });
}

TEST_CASE("SER-01 Generated data asset constructor reads typed package to native fields", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const json data = {{"Number", {{"Value", 43}}}, {"Label", {{"Value", "packed"}}}, {"Settings", {{"Value", Field("Value", 101)}}}, {"Values", {{"Value", json::array({8, 9})}}}};
        const auto path = Package(fixture, json{{"DataAssetTypeId", 7201}, {"SerializedData", data}}.dump());
        resources::BinaryReader reader(path.string());
        const ProbeDataAsset asset(reader);
        CHECK(asset.Number == 43);
        CHECK(asset.Label == "packed");
        CHECK(asset.Settings.Value == 101);
        CHECK(asset.Values == std::vector<i32>{8, 9});
        CHECK(reader.GetPosition() == static_cast<i64>(std::filesystem::file_size(path)));
    });
}

TEST_CASE("SER-02 Generated data asset rejects missing wrong or malformed type header", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        for (const json root : {json{{"SerializedData", json::object()}}, json{{"DataAssetTypeId", 999}, {"SerializedData", json::object()}}, json{{"DataAssetTypeId", "wrong"}, {"SerializedData", json::object()}}})
        {
            CAPTURE(root.dump());
            const auto path = Package(fixture, root.dump());
            resources::BinaryReader reader(path.string());
            CHECK_THROWS(ProbeDataAsset(reader));
        }
    });
}

TEST_CASE("SER-02 Generated data asset rejects missing or nonobject serialized payload", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        for (const json root : {json{{"DataAssetTypeId", 7201}}, json{{"DataAssetTypeId", 7201}, {"SerializedData", json::array()}}, json{{"DataAssetTypeId", 7201}, {"SerializedData", nullptr}}})
        {
            const auto path = Package(fixture, root.dump());
            resources::BinaryReader reader(path.string());
            CHECK_THROWS(ProbeDataAsset(reader));
        }
    });
}

TEST_CASE("SER-02 Generated data asset rejects invalid JSON package", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto path = Package(fixture, "{broken json");
        resources::BinaryReader reader(path.string());
        CHECK_THROWS(ProbeDataAsset(reader));
    });
}

TEST_CASE("SER-01 Generated Transform setter resolves parent and updates native quaternion", "[native][serialization][generated][coverage][coverage-lifecycle][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto parent = fixture.Entity(7);
        const auto child = fixture.Entity(42);
        fixture.Registry->Get<Transform>(parent).GetLocalPosition() = {10, 0, 0};
        const json rotation = {{"x", {{"Value", 0}}}, {"y", {{"Value", 0}}}, {"z", {{"Value", 90}}}};
        const json position = {{"x", {{"Value", 1}}}, {"y", {{"Value", 2}}}, {"z", {{"Value", 3}}}};
        fixture.Manager->GetBehaviourRegistry().SetBehaviourData(child, PROBE_TRANSFORM, json{{"_parent", {{"Value", 7}}}, {"_rotation", {{"Value", rotation}}}, {"_position", {{"Value", position}}}});
        const auto& transform = fixture.Registry->Get<Transform>(child);
        CHECK(transform.GetParent() == parent);
        CheckVector(transform.GetWorldPosition(), {11, 2, 3});
        CheckVector(transform.GetRight(), {0, 1, 0});
        CHECK(fixture.Manager->GetBehaviourRegistry().GetBehaviourData(child, PROBE_TRANSFORM).at("_parent") == 7);
    });
}
