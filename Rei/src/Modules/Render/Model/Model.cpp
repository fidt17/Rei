#include "pch.h"
#include "Model.h"

namespace rei::render
{

    Model::Model(resources::BinaryReader& reader)
    {
        _name = reader.GetStr();

        const i32 meshCount = reader.GetI32();

        for (int i = 0; i < meshCount; i++)
        {
            Mesh mesh(reader);
            _meshes.push_back(mesh);
        }
    }

    Model::Model(std::string name, Mesh mesh)
        : _name(std::move(name))
    {
        mesh.PostLoad();
        _meshes.push_back(mesh);
    }

    Model::Model(std::string name, std::vector<Mesh>& meshes)
        : _name(std::move(name))
    {
        for (auto& mesh : meshes)
        {
            mesh.PostLoad();
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

    void Model::PostLoad()
    {
        for (auto& mesh : _meshes)
        {
            mesh.PostLoad();
        }
    }

    const std::vector<Mesh>& Model::GetMeshes() const
    {
        return _meshes;
    }
}
