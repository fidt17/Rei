#include "pch.h"
#include "Scene.h"

namespace rei::scenes
{
    Scene::Scene(resources::BinaryReader& reader)
    {
        const auto str = reader.GetStr();
        nlohmann::json data = nlohmann::json::parse(str);

        _name = data.at("Name");
    }
}
