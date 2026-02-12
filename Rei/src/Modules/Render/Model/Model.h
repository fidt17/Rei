#pragma once
#include "Modules/Render/Mesh/Mesh.h"

namespace rei::render
{
    class Model
    {
    public:
        REI_API Model(resources::BinaryReader& reader);
        REI_API Model(std::string name, Mesh mesh);
        REI_API Model(std::string name, std::vector<Mesh>& meshes);
        REI_API ~Model();
        REI_API void PostLoad();

        const std::vector<Mesh>& GetMeshes() const;

    private:
        std::string _name;
        std::vector<Mesh> _meshes;
    };
}
