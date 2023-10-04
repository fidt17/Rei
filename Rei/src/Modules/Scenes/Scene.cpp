#include "pch.h"
#include "Scene.h"

namespace rei::scenes
{
    Scene::Scene(assets::BinaryReader& reader)
    {
        LOG("SCENE: " + reader.GetStr());
    }
}
