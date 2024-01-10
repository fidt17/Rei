#pragma once

class SceneEntity
{
public:
    explicit SceneEntity(nlohmann::json json);

    i32 GetId() const;
    const std::string& GetName() const;
    
private:
    i32 _id;
    std::string _name;
};
