#pragma once

namespace rei::scenes
{
    class Scene
    {
    public:
        explicit Scene(resources::BinaryReader& reader);

        const std::string& GetName() const { return _name; }
        
    private:
        std::string _name;
    };
}
