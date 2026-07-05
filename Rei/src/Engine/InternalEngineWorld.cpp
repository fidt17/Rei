#include "pch.h"
#include "InternalEngineWorld.h"

#include "Engine.h"
#include "Engine/Services.h"
#include "Common/Diagnostics/Systems/DiagnosticsRunnerSystem.h"
#include "Common/Time/Stopwatch.h"
#include "Ecs/Systems/DeleteHere.h"
#include "Modules/Behaviour/Components/StartBehavioursEvent.h"
#include "Modules/Behaviour/Systems/StartBehavioursSystem.h"
#include "Modules/Behaviour/Systems/UpdateBehavioursSystem.h"
#include "Modules/Editor/Systems/FlyCameraSystem.h"
#include "Modules/Editor/Systems/PointerEntitySelectionSystem.h"
#include "Modules/Editor/TransformationControls/Systems/Core/TransformationControlsModule.h"
#include "Modules/Input/Pointer/PointerCollisionSystem.h"
#include "Modules/Input/Pointer/UIPointerCollisionSystem.h"
#include "Modules/Render/Camera/AssignMainCameraSystem.h"
#include "Modules/Render/Systems/DebugOverlayToggleSystem.h"
#include "Modules/Window/WindowManager.h"

namespace rei::internal::engine
{
    void InternalEngineWorld::Configure(const std::shared_ptr<App>& app, const std::shared_ptr<render::Renderer>& renderer,
                                        const std::shared_ptr<TaskExecutor>& mainThread,
                                        const std::shared_ptr<EntityManager>& entityManager) const
    {
        struct FrameTimings
        {
            f32 WindowTimeMs = 0.0f;
            f32 UpdateTimeMs = 0.0f;
        };

        const auto frameTimings = std::make_shared<FrameTimings>();
        auto updateStopwatch = std::make_shared<time::Stopwatch>();

        _world->AddSystem([frameTimings, updateStopwatch]
        {
            time::Stopwatch windowStopwatch;
            windowStopwatch.Start();
            GetWindowManager().OnUpdate();
            windowStopwatch.Stop();
            frameTimings->WindowTimeMs = windowStopwatch.ElapsedMs();
            updateStopwatch->Start();
        });

        _world->AddSystem<common::diagnostics::DiagnosticsRunnerSystem>();

        if (GetEngine().IsPlaymode())
        {
            _world->AddSystem<behaviour::StartBehavioursSystem>(entityManager);
            _world->AddSystem<ecs::DeleteHere<StartBehavioursEvent>>();
        }

        _world->AddSystem<render::DebugOverlayToggleSystem>();

        _world->AddSystem<render::AssignMainCameraSystem>(renderer);

        _world->AddSystem<input::PointerCollisionSystem>();
        _world->AddSystem<input::UIPointerCollisionSystem>();

        if (GetEngine().IsPlaymode())
        {
            _world->AddSystem<behaviour::UpdateBehavioursSystem>(entityManager);
            _world->AddSystem([&] { app->OnUpdate(); });
        }

        if (GetEngine().IsEditorMode())
        {
            _world->AddSystem<editor::FlyCameraSystem>();
            _world->AddSystem<editor::PointerEntitySelectionSystem>();

            _world->AddModule<editor::TransformationControlsModule>();
        }

        _world->AddSystem([updateStopwatch]
        {
            updateStopwatch->Stop();
        });

        _world->AddSystem([renderer]
        {
            renderer->Render();
        });

        _world->AddSystem([mainThread, frameTimings, updateStopwatch]
        {
            updateStopwatch->Stop();
            mainThread->CompleteTasks();

            common::diagnostics::DiagnosticsService::ExecutionTimes executionTimes = {};
            executionTimes.WindowTimeMs = frameTimings->WindowTimeMs;
            executionTimes.UpdateTimeMs = updateStopwatch->ElapsedMs();
            GetDiagnostics().SetExecutionTimes(executionTimes);
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
