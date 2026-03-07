#include "pch.h"
#include "InternalEngineWorld.h"

#include "Engine.h"
#include "Engine/Services.h"
#include "Common/Diagnostics/Systems/DiagnosticsRunnerSystem.h"
#include "Ecs/Systems/DeleteHere.h"
#include "Modules/Behaviour/Components/StartBehavioursEvent.h"
#include "Modules/Behaviour/Systems/StartBehavioursSystem.h"
#include "Modules/Behaviour/Systems/UpdateBehavioursSystem.h"
#include "Modules/Editor/Systems/FlyCameraSystem.h"
#include "Modules/Editor/Systems/PointerEntitySelectionSystem.h"
#include "Modules/Editor/TransformationControls/Systems/TransformationControlsModule.h"
#include "Modules/Physics/Systems/PointerCollisionSystem.h"
#include "Modules/Render/Camera/AssignMainCameraSystem.h"
#include "Modules/Render/Systems/DebugOverlayToggleSystem.h"
#include "Modules/Window/WindowManager.h"

namespace rei::internal::engine
{
    void InternalEngineWorld::Configure(const std::shared_ptr<App>& app, const std::shared_ptr<render::Renderer>& renderer,
                                        const std::shared_ptr<TaskExecutor>& mainThread,
                                        const std::shared_ptr<EntityManager>& entityManager) const
    {
        using clock = std::chrono::high_resolution_clock;

        struct FrameTimings
        {
            clock::time_point FrameStart = {};
            float RenderTimeMs = 0.0f;
        };

        const auto frameTimings = std::make_shared<FrameTimings>();

        _world->AddSystem([frameTimings]
        {
            frameTimings->FrameStart = clock::now();
            frameTimings->RenderTimeMs = 0.0f;
            GetWindowManager().OnUpdate();
        });

        _world->AddSystem<common::diagnostics::DiagnosticsRunnerSystem>();

        if (GetEngine().IsPlaymode())
        {
            _world->AddSystem<behaviour::StartBehavioursSystem>(entityManager);
            _world->AddSystem<ecs::DeleteHere<StartBehavioursEvent>>();

            _world->AddSystem<behaviour::UpdateBehavioursSystem>(entityManager);
            _world->AddSystem([&] { app->OnUpdate(); });
        }

        _world->AddSystem<render::DebugOverlayToggleSystem>();

        _world->AddSystem<render::AssignMainCameraSystem>(renderer);

        _world->AddSystem<physics::PointerCollisionSystem>();

        if (GetEngine().IsEditor())
        {
            _world->AddSystem<editor::FlyCameraSystem>();
            _world->AddSystem<editor::PointerEntitySelectionSystem>();

            _world->AddModule<editor::TransformationControlsModule>();
        }

        _world->AddSystem([renderer, frameTimings]
        {
            const auto renderStart = clock::now();
            renderer->Render();
            const auto renderEnd = clock::now();
            frameTimings->RenderTimeMs = static_cast<float>(std::chrono::duration<double, std::milli>(renderEnd - renderStart).count());
        });

        _world->AddSystem([mainThread, frameTimings]
        {
            mainThread->CompleteTasks();

            const auto frameEnd = clock::now();
            const auto totalFrameTimeMs = static_cast<float>(std::chrono::duration<double, std::milli>(frameEnd - frameTimings->FrameStart).count());
            const auto coreTimeMs = totalFrameTimeMs > frameTimings->RenderTimeMs
                ? totalFrameTimeMs - frameTimings->RenderTimeMs
                : 0.0f;
            GetDiagnostics().SetExecutionTimes(coreTimeMs, frameTimings->RenderTimeMs);
        });
        
        LOG_DEBUG("Configured internal world")
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
