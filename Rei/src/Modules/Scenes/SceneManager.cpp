#include "pch.h"
#include "SceneManager.h"

#include "BuildScenesConfig.h"
#include "Scene.h"
#include "Engine/Services.h"
#include "Modules/Assets/AssetManager.h"

namespace rei::scenes
{
    class Scene;

    SceneManager::SceneManager()
        : _buildScenesConfig(GetAssetManager().LoadById<BuildScenesConfig>("0"))
    {
    }

    void SceneManager::LoadScene(const int id)
    {
        REI_THROW_IF(!_buildScenesConfig.Has(id), "Scene with id [" + STRING(id) + "] is missing from build scenes")

        const auto& sceneRef = _buildScenesConfig.GetScene(id);
        LOG("Loading scene ["+STRING(id)+"]. Ref: ["+sceneRef.AssetId.Id+"]")

        auto scene = GetAssetManager().Load<Scene>(sceneRef);
    }
}
