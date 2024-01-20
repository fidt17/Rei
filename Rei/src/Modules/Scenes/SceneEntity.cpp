#include "pch.h"
#include "SceneEntity.h"

SceneEntity::SceneEntity(nlohmann::json json) :
    _id(json.at("Id")),
    _name(json.at("Name"))
{
    for (auto b : json.at("Behaviours"))
    {
        _behaviours.push_back(b);
    }
}

i32 SceneEntity::GetId() const { return _id; }

const std::string& SceneEntity::GetName() const { return _name; }

const std::vector<nlohmann::json>& SceneEntity::GetBehaviours() const { return _behaviours; }
