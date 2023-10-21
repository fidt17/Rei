#include "pch.h"
#include "Scene.h"

namespace rei::scenes
{
    Scene::Scene(resources::BinaryReader& reader)
    {
        LOG("SCENE: " + reader.GetStr());
    }
}
