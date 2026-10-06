#pragma once
#include "Engine/Engine.h"
#include "Engine/Services.h"
#include "Modules/Editor/Components/EditorSelectionCollider.h"
#include "Modules/Editor/Components/SelectableByPointerTag.h"
#include "Modules/Editor/Components/SelectionByPointerBlockerTag.h"
#include "Modules/Physics/ModelCollider.h"
#include "Modules/Physics/PointerCollisionListener.h"

namespace rei::editor::renderer_selection
{
    inline std::shared_ptr<physics::ModelCollider> CreateCollider(const assets::AssetRef<render::Model>& model)
    {
        auto collider = std::make_shared<physics::ModelCollider>();
        collider->SetModel(model);
        return collider;
    }

    inline void Configure(const ecs::Entity entity, const assets::AssetRef<render::Model>& model)
    {
        if (!GetEngine().IsEditor()) return;
        ECS_WORLD(GetInternalWorld())

        if (GetEngine().IsEditorMode())
        {
            // Controls own their continuous collider explicitly at creation.
            if (HAS(entity, SelectionByPointerBlockerTag)) return;
            // Bind unloaded references too: a new model must never retain the old collider.
            GET(entity, EditorSelectionCollider).Collider = CreateCollider(model);
        }
        else
        {
            // Preserve the existing embedded Play auto-listener contract.
            if (!model.IsLoaded()) return;
            GET(entity, physics::PointerCollisionListener).Collider = CreateCollider(model);
        }
        GET(entity, SelectableByPointerTag);
    }
}
