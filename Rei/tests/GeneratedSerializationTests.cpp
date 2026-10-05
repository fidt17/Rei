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

    std::filesystem::path Package(BehaviourFixture& fixture, const std::string& body)
    {
        const auto path = fixture.Files.Write("asset.bin", {});
        resources::BinaryWriter writer(path.string(), 0);
        writer.WriteStr(body);
        writer.Close();
        return path;
    }
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
        BehaviourFixture fixture;
        const auto entity = fixture.Entity();
        fixture.Add(entity, PROBE_A, false);
        auto created = fixture.Assets->CreateAssetWithId<ProbeDataAsset>("data-a");
        fixture.Manager->GetBehaviourRegistry().SetBehaviourData(entity, PROBE_A, Field("Assets", json::array({Field("Id", "data-a")})));
        auto& probe = fixture.Registry->Get<ProbeA>(entity);
        REQUIRE(probe.Assets.size() == 1);
        REQUIRE(probe.Assets[0].IsLoaded());
        CHECK(probe.Assets[0].Get() == created.Get());
        fixture.Assets->Release(created);
        REQUIRE(created.IsLoaded());
        fixture.Manager->GetBehaviourRegistry().SetBehaviourData(entity, PROBE_A, Field("Assets", json::array()));
        CHECK(probe.Assets.empty());
        CHECK_FALSE(created.IsLoaded());
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
        const i64 overflow = static_cast<i64>(std::numeric_limits<i32>::max()) + 1;
        CHECK_THROWS(probe.REI_SET(Field("Number", overflow)));
        CHECK(probe.Number == 7);
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
