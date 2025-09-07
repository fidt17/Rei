#include "pch.h"
#include "Model.h"

#include "Common/Time/ScopedTimer.h"

namespace rei::render
{
    Model::Model(resources::BinaryReader& reader)
    {
        Name = reader.GetStr();
        time::ScopedTimer timer("Model " + Name + " loading");
        
        const i32 meshCount = reader.GetI32();
        for (int i = 0; i < meshCount; i++)
        {
            Mesh mesh(reader);
            mesh.Setup();
            _meshes.push_back(mesh);
        }
    }

    Model::~Model()
    {
        for (auto& value : _meshes)
        {
            value.Dispose();
        }
    }

    const std::vector<Mesh>& Model::GetMeshes() const
    {
        return _meshes;
    }
}
