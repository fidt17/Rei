#include "pch.h"
#include "Model.h"

#include "Common/Time/ScopedTimer.h"

namespace rei::render
{
    Model::Model(resources::BinaryReader& reader)
    {
        _name = reader.GetStr();
        time::ScopedTimer timer(std::format("Model {} loading", _name));

        const i32 meshCount = reader.GetI32();

        for (int i = 0; i < meshCount; i++)
        {
            Mesh mesh(reader);
            {
                mesh.Setup();
            }
            _meshes.push_back(mesh);
        }
    }

    Model::Model(std::string name, Mesh mesh)
        : _name(std::move(name))
    {
        mesh.Setup();
        _meshes.push_back(mesh);
    }

    Model::Model(std::string name, std::vector<Mesh>& meshes)
        : _name(std::move(name))
    {
        for (auto& mesh : meshes)
        {
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
