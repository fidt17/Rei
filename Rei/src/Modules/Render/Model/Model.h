#pragma once
#include "Modules/Render/Mesh/Mesh.h"

namespace rei::render
{
    class Model
    {
    public:
        REI_API Model(resources::BinaryReader& reader);

        const std::vector<Mesh>& GetMeshes() const;

    private:
        std::vector<Mesh> _meshes;
    };
}
