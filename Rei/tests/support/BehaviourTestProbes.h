#pragma once
#include "Core.h"
#include "Modules/EntityManagement/EntityManager.h"
#include "rei_behaviours/transformation/Transform.h"
#include <functional>
#include <memory>
#include <vector>

namespace rei::tests
{
    constexpr i32 PROBE_A = 7101;
    constexpr i32 PROBE_B = 7102;
    constexpr i32 PROBE_C = 7103;
    constexpr i32 PROBE_D = 7104;
    constexpr i32 PROBE_TRANSFORM = 7199;

    struct TraceState
    {
        std::vector<std::string> Calls;
        std::function<void(i32, const std::string&, ecs::Entity)> Action;
    };

    inline std::shared_ptr<TraceState> CurrentTrace;

    inline void Trace(const i32 id, const std::string& stage, const ecs::Entity entity)
    {
        if (!CurrentTrace) return;
        CurrentTrace->Calls.push_back(std::to_string(id) + ":" + stage);
        const auto action = CurrentTrace->Action;
        if (action) action(id, stage, entity);
    }

    class ProbeBehaviour : public Behaviour
    {
    public:
        ProbeBehaviour() = default;
        ProbeBehaviour(const i32 id, const ecs::Entity entity) : Behaviour(id, entity) {}
        void LoadAssets(assets::AssetManager&) override { Trace(GetBehaviourId(), "LoadAssets", GetEntity()); }
        void Init() override { Trace(GetBehaviourId(), "Init", GetEntity()); }
        void Start() override { Trace(GetBehaviourId(), "Start", GetEntity()); }
        void Update() override { Trace(GetBehaviourId(), "Update", GetEntity()); }
        void Dispose() override { Trace(GetBehaviourId(), "Dispose", GetEntity()); }
        void BeforeREI_GET() override { Trace(GetBehaviourId(), "BeforeGet", GetEntity()); }
        void AfterREI_SET() override { Trace(GetBehaviourId(), "AfterSet", GetEntity()); }
    };

    enum class ProbeMode { Cold = 0, Warm = 2, Hot = 5 };

    struct ProbeNested
    {
        SERIALIZABLE_BODY(ProbeNested)
        SERIALIZE i32 Value = 11;
        SERIALIZE std::string Label = "nested";
    };

    struct ProbeDataAsset
    {
        DATA_ASSET_BODY(ProbeDataAsset)
    public:
        SERIALIZE i32 Number = 7;
        SERIALIZE std::string Label = "asset";
        SERIALIZE ProbeNested Settings;
        SERIALIZE std::vector<i32> Values = {1, 2};
    };

    // Explicit metadata in Generator/Program.cs selects these public test fields.
    // Production generator supplies all REI_GET/REI_SET/ResolveDependencies bodies.
#define REI_TEST_PROBE_BODY(TYPE) \
        TYPE() = default; \
        TYPE(const i32 id, const ecs::Entity entity) : ProbeBehaviour(id, entity) {} \
        nlohmann::json REI_GET() const; \
        void REI_SET(const nlohmann::json& data); \
        void ResolveDependencies();

    struct ProbeA : ProbeBehaviour
    {
        REI_TEST_PROBE_BODY(ProbeA)
        SERIALIZE i32 Number = 7;
        SERIALIZE f32 Ratio = 1.25f;
        SERIALIZE bool Flag = true;
        SERIALIZE std::string Text = "seed";
        SERIALIZE ProbeMode Mode = ProbeMode::Warm;
        SERIALIZE ProbeNested Settings;
        SERIALIZE std::vector<i32> Values = {1, 2};
        SERIALIZE std::vector<ProbeMode> Modes;
        SERIALIZE std::vector<ProbeNested> Items;
        SERIALIZE assets::AssetRef<ProbeDataAsset> Asset;
        SERIALIZE std::vector<assets::AssetRef<ProbeDataAsset>> Assets;
        SERIALIZE ecs::ComponentRef<Transform> Target;
        SERIALIZE std::vector<ecs::ComponentRef<Transform>> Targets;
    };

    struct ProbeB : ProbeBehaviour
    {
        REI_TEST_PROBE_BODY(ProbeB)
        SERIALIZE i32 Number = 97;
    };

    struct ProbeC : ProbeBehaviour { REI_TEST_PROBE_BODY(ProbeC) };
    struct ProbeD : ProbeBehaviour { REI_TEST_PROBE_BODY(ProbeD) };
#undef REI_TEST_PROBE_BODY
}

void ConfigureComponentsFactory(rei::BehaviourRegistry& registry);
