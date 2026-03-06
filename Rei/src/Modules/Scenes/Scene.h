#pragma once
#include "SceneEntity.h"

namespace rei::scenes
{
    class Scene
    {
    public:
        explicit Scene(resources::BinaryReader& reader);

        const std::string& GetName() const;
        const std::vector<SceneEntity>& GetEntities() const;

    private:
        std::string _name;
        std::vector<SceneEntity> _entities;
    };
}
