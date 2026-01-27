#include "pch.h"
#include "InternalEngineWorld.h"

#include "Engine.h"
#include "Ecs/Systems/DeleteHere.h"
#include "Modules/Behaviour/Components/StartBehavioursEvent.h"
#include "Modules/Behaviour/Systems/StartBehavioursSystem.h"
#include "Modules/Behaviour/Systems/UpdateBehavioursSystem.h"
#include "Modules/Editor/Systems/FlyCameraSystem.h"
#include "Modules/Editor/Systems/PointerEntitySelectionSystem.h"
#include "Modules/Editor/TransformationControls/Systems/TransformationControlsModule.h"
#include "Modules/Physics/Systems/PointerCollisionSystem.h"
#include "Modules/Render/Camera/AssignMainCameraSystem.h"
#include "Modules/Window/WindowManager.h"

namespace rei::internal::engine
{
    void InternalEngineWorld::Configure(const std::shared_ptr<App>& app, const std::shared_ptr<render::Renderer>& renderer,
                                        const std::shared_ptr<TaskExecutor>& mainThread,
                                        const std::shared_ptr<EntityManager>& entityManager) const
    {
        _world->AddSystem([&] { GetWindowManager().OnUpdate(); });

        if (GetEngine().IsPlaymode())
        {
            _world->AddSystem<behaviour::StartBehavioursSystem>(entityManager);
            _world->AddSystem<ecs::DeleteHere<StartBehavioursEvent>>();

            _world->AddSystem<behaviour::UpdateBehavioursSystem>(entityManager);
            _world->AddSystem([&] { app->OnUpdate(); });
        }

        _world->AddSystem<render::AssignMainCameraSystem>(renderer);

        _world->AddSystem<physics::PointerCollisionSystem>();

        if (GetEngine().IsEditor())
        {
            _world->AddSystem<editor::FlyCameraSystem>();
            _world->AddSystem<editor::PointerEntitySelectionSystem>();

            _world->AddModule<editor::TransformationControlsModule>();
        }

        _world->AddSystem([&] { renderer->Render(); });

        _world->AddSystem([&] { mainThread->CompleteTasks(); });
    }

    void InternalEngineWorld::Run() const
    {
        _world->Run();
    }

    std::shared_ptr<ecs::World> InternalEngineWorld::GetWorld()
    {
        return _world;
    }
}
