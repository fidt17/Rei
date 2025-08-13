#include "pch.h"
#include "Model.h"

#include "assimp/Importer.hpp"

namespace rei::render
{
    Model::Model(resources::BinaryReader& reader)
    {
        const i32 meshCount = reader.GetI32();
        for (int i = 0; i < meshCount; i++)
        {
            Mesh mesh(reader);
            _meshes.push_back(mesh);
        }
    }

    const std::vector<Mesh>& Model::GetMeshes() const
    {
        return _meshes;
    }
}
