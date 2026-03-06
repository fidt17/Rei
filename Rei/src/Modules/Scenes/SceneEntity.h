#pragma once

class SceneEntity
{
public:
    explicit SceneEntity(nlohmann::json json);

    i32 GetId() const;
    const std::string& GetName() const;
    const std::vector<nlohmann::json>& GetBehaviours() const;
    
private:
    i32 _id;
    std::string _name;
    std::vector<nlohmann::json> _behaviours;
};
