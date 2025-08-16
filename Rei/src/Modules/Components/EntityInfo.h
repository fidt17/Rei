#pragma once

struct EntityInfo
{
    i32 Id;
    std::string Name;
    std::vector<i32> Behaviours {};
};
EXPORT_COMPONENT(EntityInfo);