#include "pch.h"
#include "SelectEntityWithCursorSystem.h"

#include "SelectionCollider.h"
#include "../../../resources/rei_behaviours/render/RenderOutlineTag.h"
#include "../../../resources/rei_behaviours/render/camera/Camera.h"
#include "../../../resources/rei_behaviours/transformation/Transform.h"
#include "Modules/Components/EntityInfo.h"
#include "Modules/Input/Input.h"

rei::editor::SelectEntityWithCursorSystem::SelectEntityWithCursorSystem(const std::shared_ptr<ecs::EcsRegistry>& ecs,
                                                                        const std::shared_ptr<ecs::FilterProvider>& filters): System(ecs, filters),
    _checkEntities(filters->Get<SelectionCollider>()),
    _selectedEntities(GetInternalWorld().GetFiltersRegistry()->Get<SelectedTag>())
{
}

void rei::editor::SelectEntityWithCursorSystem::ResetAllEntitiesSelection()
{
    FOR(e, _selectedEntities)
    {
        DEL(e, rei::editor::SelectedTag);
        DEL(e, rei::render::RenderOutlineTag);
    }
}

void rei::editor::SelectEntityWithCursorSystem::OnUpdate()
{
    if (!Input::IsMouseButtonReleased(GLFW_MOUSE_BUTTON_LEFT)) return;

    const auto camera = render::Camera::GetMainCamera();
    if (camera.IsNull()) return;

    f32 xPos, yPos;
    Input::GetMousePosition(xPos, yPos);
    const auto ray = camera.Get().GetScreenPointToRay(xPos, yPos);

    FOR(e, _checkEntities)
    {
        if (HAS(e, SelectedTag)) continue;

        auto& [Id, Name] = GET(e, EntityInfo);
        auto& transform = GET(e, transformation::Transform);
        auto& [Collider] = GET(e, SelectionCollider);

/// TODODO : https://chat.deepseek.com/a/chat/s/90d38609-d17f-447a-8a69-58211d05c9c3
        
        if (Collider->Intersect(ray, transform.GetPosition(), transform.CalculateModelMatrix()))
        {
            ResetAllEntitiesSelection();

            GET(e, rei::editor::SelectedTag);
            GET(e, render::RenderOutlineTag);
            return;
        }
    }

    ResetAllEntitiesSelection();
}
