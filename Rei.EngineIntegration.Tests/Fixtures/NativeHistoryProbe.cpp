#include "Startup/AppEntryPoint.h"
#include "Modules/Editor/Actions/EditorActionStore.h"
#include "Modules/Editor/Actions/EntityMoveAction.h"
#include <fstream>

class TestApplication final : public rei::App
{
    i32 _step = 0;
    u64 _lastActionId = 0;

public:
    void OnStart() override
    {
        rei::GetInternalWorld()->AddSystem([this]
        {
            std::ifstream input(R"(__COMMAND__)");
            if (!input) return;
            const auto command = nlohmann::json::parse(input, nullptr, false);
            if (command.is_discarded() || command.value("Step", 0) <= _step) return;
            _step = command.at("Step");
            ECS_WORLD(rei::GetInternalWorld());
            const auto entity = rei::GetEntityManager().GetBySceneId(2);
            if (IS_DEAD(entity)) return;
            auto& transform = GET(entity, rei::Transform);
            if (command.value("Action", "read") == "move")
            {
                const auto before = transform.GetLocalPosition();
                transform.GetLocalPosition().x = command.at("X").get<f32>();
                _lastActionId = rei::GetEngine().GetEditorActions().RecordApplied(
                    std::make_unique<rei::editor::EntityMoveAction>(rei::ecs::ComponentRef<rei::Transform>(_ecs, entity), before, transform.GetLocalPosition()));
            }
            const auto position = transform.GetLocalPosition();
            const nlohmann::json state = {{"Step", _step}, {"ActionId", _lastActionId}, {"Name", GET(entity, EntityInfo).Name}, {"Position", {position.x, position.y, position.z}}};
            std::ofstream output(R"(__STATE__)");
            output << state.dump();
        });
    }
};

std::shared_ptr<rei::App> CreateApp() { return std::make_shared<TestApplication>(); }
