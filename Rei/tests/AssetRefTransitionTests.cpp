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

TEST_CASE("REF-01 Copy constructor is a non acquiring record view", "[native][assets][coverage][coverage-remaining][isolated]")
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
        CHECK_FALSE(copy.IsLoaded());
        CHECK(copy.Get() == nullptr);
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
