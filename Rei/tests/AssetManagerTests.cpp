#include "pch.h"
#include "support/AssetTestSupport.h"
#include "Modules/Assets/Types/TextAsset.h"
#include <barrier>

using namespace rei;
using namespace rei::assets;
using namespace rei::tests;
using nlohmann::json;

TEST_CASE("ASM-01 Map names and packed offsets load independent native values", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture([](TemporaryDirectory& files) { WriteAssetPack(files, {{"a", "first", IntegerAsset(123)}, {"b", "second", IntegerAsset(-17)}}); });
        auto a = fixture.Assets->GetById<AssetProbe>("a");
        auto b = fixture.Assets->GetById<AssetProbe>("b");
        REQUIRE(a.IsLoaded());
        REQUIRE(b.IsLoaded());
        CHECK(a.Get()->Value == 123);
        CHECK(b.Get()->Value == -17);
        CHECK(a.GetName() == "first");
        CHECK(b.GetName() == "second");
        CHECK(a.GetAssetSize() == 4);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 8);
        CHECK(fixture.Assets->GetLoadedAssetCount() == 2);
    });
}

TEST_CASE("ASM-01 Repeated acquisitions reuse payload and require balanced releases", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture([](TemporaryDirectory& files) { WriteAssetPack(files, {{"a", "first", IntegerAsset(37)}}); });
        auto a = fixture.Assets->GetById<AssetProbe>("a");
        auto b = fixture.Assets->GetById<AssetProbe>("a");
        REQUIRE(a.IsLoaded());
        CHECK(a.Get() == b.Get());
        CHECK(probe.State->Constructed == 1);
        CHECK(probe.State->PostLoads == 2); // PostLoad is per successful Load, not once per lifetime.
        fixture.Assets->Release(a);
        CHECK(b.IsLoaded());
        CHECK(probe.State->Destroyed == 0);
        fixture.Assets->Release(b);
        CHECK_FALSE(a.IsLoaded());
        CHECK(probe.State->Destroyed == 1);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 0);
    });
}

TEST_CASE("ASM-01 Retained ref reloads same record with a new owned payload", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture([](TemporaryDirectory& files) { WriteAssetPack(files, {{"a", "first", IntegerAsset(41)}}); });
        auto ref = fixture.Assets->GetById<AssetProbe>("a");
        const auto record = ref.Record;
        fixture.Assets->Release(ref);
        REQUIRE_FALSE(ref.IsLoaded());
        REQUIRE(fixture.Assets->Load(ref));
        CHECK(ref.Record == record);
        CHECK(ref.Get()->Value == 41);
        CHECK(probe.State->Constructed == 2);
        CHECK(probe.State->Destroyed == 1);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 4);
    });
}

TEST_CASE("ASM-01 Runtime assets use unique IDs and zero packaged byte accounting", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture;
        auto a = fixture.Assets->CreateAsset<AssetProbe>(11);
        auto b = fixture.Assets->CreateAsset<AssetProbe>(22);
        REQUIRE(a.IsLoaded());
        REQUIRE(b.IsLoaded());
        CHECK_FALSE(a.Id.empty());
        CHECK(a.Id != b.Id);
        CHECK(a.Get()->Value == 11);
        CHECK(b.Get()->Value == 22);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 0);
        CHECK(fixture.Assets->GetLoadedAssetCount() == 2);
    });
}

TEST_CASE("ASM-01 Empty and missing IDs fail without loading or mutating read output", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture;
        for (const std::string id : {"", "missing"})
        {
            AssetRef<AssetProbe> ref(id);
            CHECK_FALSE(fixture.Assets->Load(ref));
            CHECK_FALSE(ref.IsLoaded());
            json output = {{"sentinel", 19}};
            CHECK_FALSE(fixture.Assets->TryGetLoadedAssetData(id, output));
            CHECK(output == json{{"sentinel", 19}});
            CHECK_FALSE(fixture.Assets->TrySetLoadedAssetData(id, json{{"Value", 22}}));
            CHECK(fixture.Assets->InspectLoadedAsset(id) == json{{"status", "unloaded"}, {"values", nullptr}});
        }
        CHECK(probe.State->Attempts == 0);
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
    });
}

TEST_CASE("ASM-02 Constructor failure leaves no phantom record and disk retry succeeds", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture([](TemporaryDirectory& files) { WriteAssetPack(files, {{"a", "first", IntegerAsset(-99)}}); });
        AssetRef<AssetProbe> ref("a");
        CHECK_FALSE(fixture.Assets->Load(ref));
        CHECK_FALSE(ref.IsLoaded());
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 0);
        WriteAssetPack(fixture.Files, {{"a", "first", IntegerAsset(71)}});
        REQUIRE(fixture.Assets->Load(ref));
        CHECK(ref.Get()->Value == 71);
        CHECK(probe.State->Attempts == 2);
        CHECK(probe.State->Constructed == 1);
    });
}

TEST_CASE("ASM-02 PostLoad failure marks Failed and explicit release destroys payload", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        probe.State->ThrowPostLoad = true;
        BehaviourFixture fixture([](TemporaryDirectory& files) { WriteAssetPack(files, {{"a", "first", IntegerAsset(13)}}); });
        AssetRef<AssetProbe> ref("a");
        CHECK_FALSE(fixture.Assets->Load(ref));
        REQUIRE(ref.Record != nullptr);
        CHECK(ref.Record->State == AssetState::Failed);
        CHECK_FALSE(ref.IsLoaded());
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 4); // Characterize retained failed payload.
        fixture.Assets->Release(ref);
        CHECK(probe.State->Destroyed == 1);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 0);
    });
}

TEST_CASE("ASM-02 Retry after Failed replaces payload without double byte accounting", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        probe.State->ThrowPostLoad = true;
        BehaviourFixture fixture([](TemporaryDirectory& files) { WriteAssetPack(files, {{"a", "first", IntegerAsset(13)}}); });
        AssetRef<AssetProbe> ref("a");
        REQUIRE_FALSE(fixture.Assets->Load(ref));
        probe.State->ThrowPostLoad = false;
        REQUIRE(fixture.Assets->Load(ref));
        CHECK(ref.Get()->Value == 13);
        CHECK(probe.State->Constructed == 2);
        CHECK(probe.State->Destroyed == 1);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 4);
        fixture.Assets->Release(ref);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 0);
    });
}

TEST_CASE("ASM-02 Wrong type preserves original loaded payload and rejects new acquisition", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture([](TemporaryDirectory& files) { WriteAssetPack(files, {{"a", "first", IntegerAsset(29)}}); });
        auto original = fixture.Assets->CreateAssetWithId<UnsupportedAsset>("a");
        const auto payload = original.Get();
        AssetRef<AssetProbe> wrong("a");
        CHECK_FALSE(fixture.Assets->Load(wrong));
        CHECK_FALSE(wrong.IsLoaded());
        CHECK(original.Get() == payload);
        CHECK(fixture.Assets->GetLoadedAssetCount() == 1);
        CHECK(probe.State->Constructed == 1);
        CHECK(probe.State->Destroyed == 1);
        CHECK(fixture.Assets->GetLoadedAssetsSize() == 0);
    });
}

TEST_CASE("ASM-01 Inspection and setter operate on actual loaded native payload", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture;
        auto ref = fixture.Assets->CreateAssetWithId<AssetProbe>("a", 37);
        CHECK(fixture.Assets->InspectLoadedAsset("a") == json{{"status", "loaded"}, {"values", {{"Value", 37}}}});
        REQUIRE(fixture.Assets->TrySetLoadedAssetData("a", json{{"Value", 91}}));
        CHECK(ref.Get()->Value == 91);
        CHECK(probe.State->Resolves == 1);
        json output;
        REQUIRE(fixture.Assets->TryGetLoadedAssetData("a", output));
        CHECK(output == json{{"Value", 91}});
        fixture.Assets->Release(ref); // Inspection/setter added no acquisition.
        CHECK_FALSE(ref.IsLoaded());
    });
}

TEST_CASE("ASM-01 Unsupported serialization is distinct from unloaded inspection", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        auto ref = fixture.Assets->CreateAssetWithId<UnsupportedAsset>("opaque");
        CHECK(fixture.Assets->InspectLoadedAsset("opaque") == json{{"status", "unsupported"}, {"values", nullptr}});
        CHECK_FALSE(fixture.Assets->TrySetLoadedAssetData("opaque", json::object()));
        CHECK(ref.IsLoaded());
        fixture.Assets->Release(ref);
        CHECK(fixture.Assets->InspectLoadedAsset("opaque").at("status") == "unloaded");
    });
}

TEST_CASE("ASM-02 Native serializer exceptions propagate without hidden reload", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture;
        auto ref = fixture.Assets->CreateAssetWithId<AssetProbe>("a", 17);
        probe.State->ThrowGet = true;
        CHECK_THROWS_AS(fixture.Assets->InspectLoadedAsset("a"), std::runtime_error);
        CHECK_THROWS(fixture.Assets->TrySetLoadedAssetData("a", json{{"Value", "wrong"}}));
        CHECK(ref.Get()->Value == 17);
        CHECK(probe.State->Constructed == 1);
        CHECK(probe.State->Resolves == 0);
    });
}

TEST_CASE("ASM-02 Cached concurrent loads balance eighty independent acquisitions", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture;
        auto owner = fixture.Assets->CreateAssetWithId<AssetProbe>("shared", 83);
        auto* const payload = owner.Get();
        std::atomic<i32> failures = 0;
        std::barrier start(4);
        std::vector<std::thread> workers;
        for (i32 i = 0; i < 4; ++i) workers.emplace_back([&]
        {
            start.arrive_and_wait();
            for (i32 j = 0; j < 20; ++j)
            {
                auto ref = fixture.Assets->GetById<AssetProbe>("shared");
                if (ref.Get() != payload || ref.Get()->Value != 83) ++failures;
                fixture.Assets->Release(ref);
            }
        });
        for (auto& worker : workers) worker.join();
        CHECK(failures == 0);
        CHECK(probe.State->Constructed == 1);
        CHECK(probe.State->PostLoads == 81);
        REQUIRE(owner.IsLoaded());
        fixture.Assets->Release(owner);
        CHECK(probe.State->Destroyed == 1);
        CHECK(fixture.Assets->GetLoadedAssetCount() == 0);
    });
}

TEST_CASE("ASM-01 Bulk unload invalidates retained refs and manager remains reusable", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        AssetProbeScope probe;
        BehaviourFixture fixture;
        auto a = fixture.Assets->CreateAssetWithId<AssetProbe>("a");
        auto b = fixture.Assets->CreateAssetWithId<AssetProbe>("b");
        fixture.Assets->UnloadAllAssets();
        fixture.Assets->UnloadAllAssets();
        CHECK_FALSE(a.IsLoaded());
        CHECK_FALSE(b.IsLoaded());
        CHECK(probe.State->Destroyed == 2);
        auto c = fixture.Assets->CreateAssetWithId<AssetProbe>("c", 93);
        REQUIRE(c.IsLoaded());
        CHECK(c.Get()->Value == 93);
        fixture.Assets->Release(c);
        CHECK(probe.State->Destroyed == 3);
    });
}

TEST_CASE("ASM-01 GetByPath packages owned text and cleans its temporary file", "[native][assets][coverage][coverage-assets][isolated]")
{
    Isolated([]
    {
        BehaviourFixture fixture;
        const auto name = fixture.Files.File("").filename().string() + "-source.txt";
        const std::string text = "native text";
        const auto path = fixture.Files.Write(name, {text.begin(), text.end()});
        auto ref = fixture.Assets->GetByPath<TextAsset>(path.string());
        REQUIRE(ref.IsLoaded());
        CHECK(ref.Get()->GetValue() == text);
        CHECK(ref.GetAssetSize() == static_cast<i32>(4 + text.size()));
        const auto packed = std::filesystem::path(std::filesystem::temp_directory_path().string() + "Rei Engine\\") / (name + "_0.data");
        REQUIRE(std::filesystem::exists(packed));
        std::vector<u8> expected;
        AppendString(expected, text);
        CHECK(ReadBytes(packed) == expected);
        auto again = fixture.Assets->GetByPath<TextAsset>(path.string());
        CHECK(again.Get() == ref.Get());
        fixture.Assets->Release(ref);
        CHECK(again.IsLoaded());
        fixture.Assets->Release(again);
        CHECK_FALSE(ref.IsLoaded());
        fixture.Assets->DeleteTmpFiles();
        CHECK_FALSE(std::filesystem::exists(packed));
        CHECK(std::filesystem::exists(path)); // Source is never deleted by manager.
    });
}
