#include "pch.h"
#include "support/NativeTestSupport.h"
#include "Modules/Assets/Registry/AssetRegistry.h"
#include <algorithm>

using namespace rei::assets;
using rei::tests::Isolated;

namespace
{
    struct Lifetime
    {
        i32 Destroyed = 0;
        std::function<void()> OnDestroy;
    };

    struct ProbeAsset
    {
        std::shared_ptr<Lifetime> LifetimeState;
        i32 Value;
        ~ProbeAsset()
        {
            ++LifetimeState->Destroyed;
            if (LifetimeState->OnDestroy) LifetimeState->OnDestroy();
        }
    };

    struct OtherAsset : ProbeAsset {};

    void Create(AssetRegistry& registry, AssetRef<ProbeAsset>& ref, const std::shared_ptr<Lifetime>& lifetime, const i32 size = 12, const i32 value = 7)
    {
        registry.CreateAssetRecord(ref, "probe", new ProbeAsset{lifetime, value}, size, AssetState::Loaded);
        registry.SetRefCount(ref.Id, 1);
    }
}

TEST_CASE("ASR-01 Typed records expose actual loaded payload and size", "[native][assets][coverage]")
{
    AssetRegistry registry;
    const auto lifetime = std::make_shared<Lifetime>();
    AssetRef<ProbeAsset> ref("asset-a");
    Create(registry, ref, lifetime);
    REQUIRE(ref.IsLoaded());
    REQUIRE(ref.Get()->Value == 7);
    REQUIRE(ref.GetName() == "probe");
    REQUIRE(ref.GetBoundId() == "asset-a");
    REQUIRE(ref.GetAssetSize() == 12);
    REQUIRE(registry.FindRecord<ProbeAsset>(ref.Id) == ref.Record);
    REQUIRE(registry.GetRecordCount() == 1);
    REQUIRE(registry.GetLoadedAssetsSize() == 12);
    CHECK(registry.FindRecord("missing") == nullptr);
    CHECK(registry.FindRecord("") == nullptr);
}

TEST_CASE("ASR-01 Empty IDs reject and destroy transferred payload", "[native][assets][coverage]")
{
    AssetRegistry registry;
    const auto lifetime = std::make_shared<Lifetime>();
    AssetRef<ProbeAsset> ref("");
    registry.CreateAssetRecord(ref, "invalid", new ProbeAsset{lifetime, 1}, 12, AssetState::Loaded);
    CHECK(lifetime->Destroyed == 1);
    CHECK_FALSE(ref.IsLoaded());
    CHECK(registry.GetRecordCount() == 0);
    CHECK(registry.GetLoadedAssetsSize() == 0);
}

TEST_CASE("ASR-01 Wrong type replacement preserves original payload", "[native][assets][coverage]")
{
    AssetRegistry registry;
    const auto originalLifetime = std::make_shared<Lifetime>();
    const auto rejectedLifetime = std::make_shared<Lifetime>();
    AssetRef<ProbeAsset> original("typed");
    Create(registry, original, originalLifetime);
    AssetRef<OtherAsset> rejected(original.Id);
    registry.CreateAssetRecord(rejected, "wrong", new OtherAsset{{rejectedLifetime, 99}}, 25, AssetState::Loaded);
    CHECK(rejectedLifetime->Destroyed == 1);
    CHECK(originalLifetime->Destroyed == 0);
    CHECK(original.Get()->Value == 7);
    CHECK_FALSE(rejected.IsLoaded());
    CHECK(registry.FindRecord<OtherAsset>(original.Id) == nullptr);
    CHECK(registry.GetLoadedAssetsSize() == 12);
}

TEST_CASE("ASR-02 Explicit acquisitions require matching releases", "[native][assets][coverage]")
{
    AssetRegistry registry;
    const auto lifetime = std::make_shared<Lifetime>();
    AssetRef<ProbeAsset> ref("shared");
    Create(registry, ref, lifetime);
    registry.IncrementRefCount(ref.Id);
    const auto first = registry.ReleaseAssetWithId(ref.Id);
    REQUIRE_FALSE(first.RefCountReachedZero);
    REQUIRE(ref.IsLoaded());
    REQUIRE(lifetime->Destroyed == 0);
    REQUIRE(registry.GetLoadedAssetsSize() == 12);
    const auto second = registry.ReleaseAssetWithId(ref.Id);
    CHECK(second.RefCountReachedZero);
    CHECK_FALSE(second.MissingLoadedRecord);
    CHECK(second.ReleasedSize == 12);
    CHECK_FALSE(ref.IsLoaded());
    CHECK(ref.Get() == nullptr);
    CHECK(ref.Record->State == AssetState::Unloaded);
    CHECK(lifetime->Destroyed == 1);
    CHECK(registry.GetLoadedAssetsSize() == 0);
}

TEST_CASE("REF-01 AssetRef copies do not acquire or release engine ownership", "[native][assets][coverage]")
{
    AssetRegistry registry;
    const auto lifetime = std::make_shared<Lifetime>();
    AssetRef<ProbeAsset> ref("copy");
    Create(registry, ref, lifetime);
    {
        AssetRef<ProbeAsset> copy(ref);
        CHECK(copy.Get() == ref.Get());
        copy = copy;
        CHECK(copy.Get()->Value == 7);
    }
    REQUIRE(ref.IsLoaded());
    REQUIRE(lifetime->Destroyed == 0);
    const AssetRef<ProbeAsset> retained(ref);
    registry.ReleaseAssetWithId(ref.Id);
    CHECK(lifetime->Destroyed == 1);
    CHECK_FALSE(retained.IsLoaded());
    CHECK(retained.Get() == nullptr);
    CHECK(retained.Record == ref.Record);
}

TEST_CASE("ASR-02 Retained records resolve a newly loaded payload", "[native][assets][coverage]")
{
    AssetRegistry registry;
    const auto oldLifetime = std::make_shared<Lifetime>();
    const auto newLifetime = std::make_shared<Lifetime>();
    AssetRef<ProbeAsset> ref("reload");
    Create(registry, ref, oldLifetime);
    const AssetRef<ProbeAsset> retained(ref);
    registry.ReleaseAssetWithId(ref.Id);
    REQUIRE_FALSE(retained.IsLoaded());
    Create(registry, ref, newLifetime, 25, 99);
    CHECK(retained.IsLoaded());
    CHECK(retained.Get()->Value == 99);
    CHECK(oldLifetime->Destroyed == 1);
    CHECK(registry.GetLoadedAssetsSize() == 25);
    registry.ReleaseAssetWithId(ref.Id);
    CHECK(newLifetime->Destroyed == 1);
    CHECK_FALSE(retained.IsLoaded());
}

TEST_CASE("ASR-02 Repeated and missing releases do not double destroy payload", "[native][assets][coverage]")
{
    AssetRegistry registry;
    const auto lifetime = std::make_shared<Lifetime>();
    AssetRef<ProbeAsset> ref("released");
    Create(registry, ref, lifetime);
    registry.ReleaseAssetWithId(ref.Id);
    CHECK(registry.ReleaseAssetWithId(ref.Id).MissingLoadedRecord);
    CHECK(registry.ReleaseAssetWithId("missing").MissingLoadedRecord);
    CHECK(lifetime->Destroyed == 1);
    CHECK(registry.GetLoadedAssetsSize() == 0);
}

TEST_CASE("ASR-03 Bulk unload invalidates every retained consumer", "[native][assets][coverage]")
{
    AssetRegistry registry;
    const auto firstLifetime = std::make_shared<Lifetime>();
    const auto secondLifetime = std::make_shared<Lifetime>();
    AssetRef<ProbeAsset> first("first");
    AssetRef<ProbeAsset> second("second");
    Create(registry, first, firstLifetime, 12);
    Create(registry, second, secondLifetime, 25);
    registry.IncrementRefCount(second.Id);
    REQUIRE(registry.GetLoadedAssetsSize() == 37);
    auto unloaded = registry.ReleaseAllLoadedAssets();
    std::sort(unloaded.begin(), unloaded.end(), [](const auto& a, const auto& b) { return a.Id < b.Id; });
    REQUIRE(unloaded.size() == 2);
    CHECK(unloaded[0].Id == "first");
    CHECK(unloaded[0].Size == 12);
    CHECK(unloaded[1].Id == "second");
    CHECK(unloaded[1].Size == 25);
    CHECK_FALSE(first.IsLoaded());
    CHECK_FALSE(second.IsLoaded());
    CHECK(firstLifetime->Destroyed == 1);
    CHECK(secondLifetime->Destroyed == 1);
    CHECK(registry.GetLoadedAssetsSize() == 0);
    CHECK(registry.ReleaseAllLoadedAssets().empty());
}

TEST_CASE("ASR-01 Duplicate payload replacement keeps byte accounting exact", "[native][assets][coverage]")
{
    AssetRegistry registry;
    const auto oldLifetime = std::make_shared<Lifetime>();
    const auto newLifetime = std::make_shared<Lifetime>();
    AssetRef<ProbeAsset> ref("duplicate");
    Create(registry, ref, oldLifetime, 12);
    Create(registry, ref, newLifetime, 25, 99);
    CHECK(oldLifetime->Destroyed == 1);
    CHECK(ref.Get()->Value == 99);
    CHECK(registry.GetLoadedAssetsSize() == 25);
    registry.ReleaseAssetWithId(ref.Id);
    CHECK(newLifetime->Destroyed == 1);
    CHECK(registry.GetLoadedAssetsSize() == 0);
}

TEST_CASE("ASR-03 Final release permits a reentrant payload destructor", "[native][assets][coverage][isolated]")
{
    Isolated([]
    {
        AssetRegistry registry;
        const auto lifetime = std::make_shared<Lifetime>();
        AssetRef<ProbeAsset> ref("reentrant");
        Create(registry, ref, lifetime);
        bool observedUnloaded = false;
        lifetime->OnDestroy = [&]
        {
            const auto record = registry.FindRecord(ref.Id);
            observedUnloaded = record != nullptr && record->State == AssetState::Unloaded && record->Value == nullptr;
        };
        registry.ReleaseAssetWithId(ref.Id);
        REQUIRE(observedUnloaded);
        REQUIRE(lifetime->Destroyed == 1);
    });
}

TEST_CASE("ASR-03 Replacement permits a reentrant payload destructor", "[native][assets][coverage][isolated]")
{
    Isolated([]
    {
        AssetRegistry registry;
        const auto oldLifetime = std::make_shared<Lifetime>();
        const auto newLifetime = std::make_shared<Lifetime>();
        AssetRef<ProbeAsset> ref("reentrant-replace");
        Create(registry, ref, oldLifetime);
        bool destructorEnteredRegistry = false;
        oldLifetime->OnDestroy = [&]
        {
            destructorEnteredRegistry = registry.FindRecord(ref.Id) != nullptr;
        };
        Create(registry, ref, newLifetime, 25, 99);
        REQUIRE(destructorEnteredRegistry);
        REQUIRE(oldLifetime->Destroyed == 1);
        registry.ReleaseAssetWithId(ref.Id);
    });
}

TEST_CASE("REF-02 External ID changes cannot expose a stale payload", "[native][assets][coverage]")
{
    AssetRegistry registry;
    const auto lifetime = std::make_shared<Lifetime>();
    AssetRef<ProbeAsset> ref("original");
    Create(registry, ref, lifetime);
    ref.Id = "different";
    CHECK_FALSE(ref.IsLoaded());
    CHECK(ref.Get() == nullptr);
    CHECK(ref.GetBoundId() == "original");
    ref.Id = "original";
    CHECK(ref.IsLoaded());
    CHECK(ref.Get()->Value == 7);
}

TEST_CASE("ASR-01 Null payloads remain unloaded with zero size", "[native][assets][coverage]")
{
    AssetRegistry registry;
    AssetRef<ProbeAsset> ref("null");
    registry.CreateAssetRecord(ref, "empty", static_cast<ProbeAsset*>(nullptr), 0, AssetState::Loaded);
    REQUIRE(ref.Record != nullptr);
    CHECK(ref.Record->State == AssetState::Unloaded);
    CHECK_FALSE(ref.IsLoaded());
    CHECK(ref.Get() == nullptr);
    CHECK(ref.GetAssetSize() == 0);
    CHECK(registry.GetLoadedAssetsSize() == 0);
    CHECK(registry.ReleaseAllLoadedAssets().empty());
}

TEST_CASE("ASR-03 Bulk unload releases records without retained references", "[native][assets][coverage]")
{
    AssetRegistry registry;
    const auto lifetime = std::make_shared<Lifetime>();
    {
        AssetRef<ProbeAsset> ref("unretained");
        Create(registry, ref, lifetime);
    }
    REQUIRE(lifetime->Destroyed == 0);
    REQUIRE(registry.GetRecordCount() == 1);
    const auto unloaded = registry.ReleaseAllLoadedAssets();
    REQUIRE(unloaded.size() == 1);
    CHECK(lifetime->Destroyed == 1);
    CHECK(registry.GetRecordCount() == 0);
    CHECK(registry.GetLoadedAssetsSize() == 0);
    CHECK(registry.FindRecord("unretained") == nullptr);
}

TEST_CASE("ASR-02 Unloaded retained IDs keep their original type", "[native][assets][coverage]")
{
    AssetRegistry registry;
    const auto originalLifetime = std::make_shared<Lifetime>();
    const auto rejectedLifetime = std::make_shared<Lifetime>();
    AssetRef<ProbeAsset> original("typed-unloaded");
    Create(registry, original, originalLifetime);
    registry.ReleaseAssetWithId(original.Id);
    REQUIRE(originalLifetime->Destroyed == 1);
    AssetRef<OtherAsset> rejected(original.Id);
    registry.CreateAssetRecord(rejected, "wrong", new OtherAsset{{rejectedLifetime, 99}}, 25, AssetState::Loaded);
    CHECK(rejectedLifetime->Destroyed == 1);
    CHECK_FALSE(rejected.IsLoaded());
    CHECK(original.Record->Type == typeid(ProbeAsset));
    CHECK(original.Record->State == AssetState::Unloaded);
    CHECK(registry.GetLoadedAssetsSize() == 0);
}

TEST_CASE("ASR-03 Registry destruction releases payloads exactly once", "[native][assets][coverage]")
{
    const auto lifetime = std::make_shared<Lifetime>();
    {
        AssetRegistry registry;
        AssetRef<ProbeAsset> ref("teardown");
        Create(registry, ref, lifetime);
        REQUIRE(lifetime->Destroyed == 0);
    }
    CHECK(lifetime->Destroyed == 1);
}
