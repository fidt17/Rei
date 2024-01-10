#include "pch.h"
#include "SceneEntity.h"

SceneEntity::SceneEntity(nlohmann::json json) :
        _id(json.at("Id")),
        _name(json.at("Name"))
{
}

i32 SceneEntity::GetId() const { return _id; }

const std::string& SceneEntity::GetName() const { return _name; }
