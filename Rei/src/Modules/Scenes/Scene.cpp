#include "pch.h"
#include "Scene.h"

namespace rei::scenes
{
    Scene::Scene(resources::BinaryReader& reader)
    {
        using namespace nlohmann;
        
        const auto str = reader.GetStr();
        json data = json::parse(str);

        _name = data.at("Name");

        json entities = data.at("Entities");
        for (const auto& e : entities)
        {
            _entities.emplace_back(e);
        }
    }

    const std::string& Scene::GetName() const
    {
        return _name;
    }

    const std::vector<SceneEntity>& Scene::GetEntities() const
    {
        return _entities;
    }
}
