#pragma once
#include "NativeTestSupport.h"
#include "BehaviourTestProbes.h"
#include "Ecs/Systems/DeleteHere.h"
#include "Modules/Behaviour/Systems/StartBehavioursSystem.h"
#include "Modules/Behaviour/Systems/UpdateBehavioursSystem.h"
#include "Modules/Behaviour/Components/StartBehavioursEvent.h"
#include "Modules/EntityManagement/EntityManager.h"
#include <algorithm>
#include <cstdlib>
#include <optional>

namespace rei::tests
{
    // Use only inside Isolated(). Owns real World, manager, registry and empty
    // resources map; no Engine/window/GL or user project is created.
    class BehaviourFixture
    {
    public:
        TemporaryDirectory Files;
        std::shared_ptr<ecs::World> World = std::make_shared<ecs::World>();
        std::shared_ptr<ecs::EcsRegistry> Registry = World->GetRegistry();
        std::shared_ptr<EntityManager> Manager = std::make_shared<EntityManager>(World);
        std::shared_ptr<assets::AssetManager> Assets;
        std::shared_ptr<TraceState> Events = std::make_shared<TraceState>();

        explicit BehaviourFixture(const std::function<void(TemporaryDirectory&)>& prepareResources = {}) : _cwd(std::filesystem::current_path()), _previousWorld(GetInternalWorld()), _previousTrace(CurrentTrace)
        {
            if (!IsIsolatedChild()) throw std::logic_error("BehaviourFixture requires an isolated native test child");
            char* resourceValue = nullptr;
            size_t resourceLength = 0;
            if (_dupenv_s(&resourceValue, &resourceLength, "REI_RESOURCES_PATH") != 0) throw std::runtime_error("Could not read resources override");
            std::unique_ptr<char, decltype(&std::free)> resourceOwner(resourceValue, &std::free);
            if (resourceValue) _resources = resourceValue;
            Files.Write("map.bin", {0, 0, 0, 0});
            _putenv_s("REI_RESOURCES_PATH", Files.File("").string().c_str());
            if (prepareResources) prepareResources(Files);
            Assets = std::make_shared<assets::AssetManager>();
            Services::GetInstance()->SetInternalWorld(World);
            Services::GetInstance()->SetEntityManager(Manager);
            Services::GetInstance()->SetAssetManager(Assets);
            CurrentTrace = Events;
            ConfigureComponentsFactory(Manager->GetBehaviourRegistry());
        }

        ~BehaviourFixture()
        {
            Events->Action = {};
            World->Refresh();
            World->RefreshAll();
            const auto entities = World->GetFiltersRegistry()->Get<EntityInfo>()->Entities();
            for (const auto entity : entities)
            {
                if (!Registry->IsAlive(entity)) continue;
                Registry->Del<ProbeA>(entity);
                Registry->Del<ProbeB>(entity);
                Registry->Del<ProbeC>(entity);
                Registry->Del<ProbeD>(entity);
                Registry->Del<Transform>(entity);
            }
            Assets->UnloadAllAssets();
            Assets->DeleteTmpFiles();
            Services::GetInstance()->SetAssetManager(nullptr);
            Services::GetInstance()->SetEntityManager(nullptr);
            Services::GetInstance()->SetInternalWorld(_previousWorld);
            assets::AssetRef<ProbeDataAsset>::AssignHandlerFunc = nullptr;
            CurrentTrace = _previousTrace;
            std::filesystem::current_path(_cwd);
            _putenv_s("REI_RESOURCES_PATH", _resources ? _resources->c_str() : "");
        }

        BehaviourFixture(const BehaviourFixture&) = delete;
        BehaviourFixture& operator=(const BehaviourFixture&) = delete;

        ecs::Entity Entity(const i32 sceneId = 42)
        {
            const auto entity = Registry->NewEntity();
            Registry->Get<EntityInfo>(entity) = {sceneId, "probe"};
            Registry->Get<Transform>(entity) = Transform(PROBE_TRANSFORM, entity);
            Registry->Get<Transform>(entity).Reset();
            World->Refresh();
            return entity;
        }

        Behaviour& Add(const ecs::Entity entity, const i32 id, const bool initialize = true)
        {
            return Manager->AddBehaviour(entity, id, nlohmann::json(), initialize);
        }

        void StartStage()
        {
            World->Refresh();
            behaviour::StartBehavioursSystem system(World, Manager);
            system.OnUpdate();
            World->Refresh();
        }

        void DeleteStartEvents()
        {
            ecs::DeleteHere<StartBehavioursEvent> system(World);
            system.OnUpdate();
            World->Refresh();
        }

        void UpdateStage()
        {
            World->Refresh();
            behaviour::UpdateBehavioursSystem system(World, Manager);
            system.OnUpdate();
            World->Refresh();
        }

        void Frame() { StartStage(); DeleteStartEvents(); UpdateStage(); }

        i32 Count(const i32 id, const std::string& stage) const
        {
            return static_cast<i32>(std::ranges::count(Events->Calls, std::to_string(id) + ":" + stage));
        }

    private:
        std::filesystem::path _cwd;
        std::optional<std::string> _resources;
        std::shared_ptr<ecs::World> _previousWorld;
        std::shared_ptr<TraceState> _previousTrace;
    };
}
