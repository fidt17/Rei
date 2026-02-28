#include "pch.h"
#include "Model.h"

#include "Common/Time/ScopedTimer.h"

namespace rei::render
{

    Model::Model(resources::BinaryReader& reader)
    {
        _name = reader.GetStr();
        time::ScopedTimer timer(std::format("Model {} loading", _name));
        LOG_DEBUG("Model deserialize start name={}", _name)

        const i32 meshCount = reader.GetI32();
        LOG_DEBUG("Model deserialize meshCount={} name={}", meshCount, _name)

        for (int i = 0; i < meshCount; i++)
        {
            Mesh mesh(reader);
            _meshes.push_back(mesh);
        }
        LOG_DEBUG("Model deserialize complete name={}, meshes={}", _name, _meshes.size())
    }

    Model::Model(std::string name, Mesh mesh)
        : _name(std::move(name))
    {
        LOG_DEBUG("Model runtime create (single mesh) name={}", _name)
        mesh.PostLoad();
        _meshes.push_back(mesh);
        LOG_DEBUG("Model runtime create complete name={}, meshes={}", _name, _meshes.size())
    }

    Model::Model(std::string name, std::vector<Mesh>& meshes)
        : _name(std::move(name))
    {
        LOG_DEBUG("Model runtime create (vector meshes) name={}, inputMeshes={}", _name, meshes.size())
        for (auto& mesh : meshes)
        {
            mesh.PostLoad();
            _meshes.push_back(mesh);
        }
        LOG_DEBUG("Model runtime create complete name={}, meshes={}", _name, _meshes.size())
    }

    Model::~Model()
    {
        LOG_DEBUG("Model destroy name={}, meshes={}", _name, _meshes.size())
        for (auto& value : _meshes)
        {
            value.Dispose();
        }
        LOG_DEBUG("Model destroy complete name={}", _name)
    }

    void Model::PostLoad()
    {
        LOG_DEBUG("Model PostLoad start name={}, meshes={}", _name, _meshes.size())
        for (auto& mesh : _meshes)
        {
            mesh.PostLoad();
        }
        LOG_DEBUG("Model PostLoad complete name={}", _name)
    }

    const std::vector<Mesh>& Model::GetMeshes() const
    {
        return _meshes;
    }
}
