#pragma once
#include "Common/Tasks/TaskExecutor.h"
#include "Modules/Render/Renderer.h"
#include "Startup/App.h"
#include "Modules/Editor/Actions/EditorActionStore.h"

namespace rei::internal::engine
{
    class InternalEngineWorld
    {
    public:
        void Configure(const std::shared_ptr<App>& app, const std::shared_ptr<render::Renderer>& renderer, const std::shared_ptr<TaskExecutor>& mainThread, const std::shared_ptr<EntityManager>& entityManager) const;
        void Run() const;

        std::shared_ptr<ecs::World> GetWorld();
        editor::EditorActionStore& GetEditorActions() const { return *_editorActions; }

    private:
        std::shared_ptr<ecs::World> _world = std::make_shared<ecs::World>();
        std::shared_ptr<editor::EditorActionStore> _editorActions = std::make_shared<editor::EditorActionStore>();
    };
}
