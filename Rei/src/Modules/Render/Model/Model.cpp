#include "pch.h"
#include "Model.h"

namespace rei::render
{

    Model::Model(resources::BinaryReader& reader)
    {
        _name = reader.GetStr();

        const i32 meshCount = reader.GetI32();
        REI_THROW_IF(meshCount < 0, "Negative packed model mesh count")

        for (i32 i = 0; i < meshCount; i++)
        {
            _meshes.emplace_back(reader);
        }
        const auto hasBounds = reader.GetU8();
        REI_THROW_IF(hasBounds > 1, "Invalid packed model bounds flag")
        if (hasBounds != 0)
        {
            const auto min = reader.GetByType<glm::vec3>();
            const auto max = reader.GetByType<glm::vec3>();
            for (u32 axis = 0; axis < 3; ++axis)
            {
                REI_THROW_IF(!std::isfinite(min[axis]) || !std::isfinite(max[axis]) || min[axis] > max[axis], "Invalid packed model bounds")
            }
            _bounds.Include(min);
            _bounds.Include(max);
        }
    }

    Model::Model(std::string name, Mesh mesh)
        : _name(std::move(name))
    {
        mesh.PostLoad();
        _meshes.push_back(mesh);
        BuildBounds();
    }

    Model::Model(std::string name, std::vector<Mesh>& meshes)
        : _name(std::move(name))
    {
        for (auto& mesh : meshes)
        {
            mesh.PostLoad();
            _meshes.push_back(mesh);
        }
        BuildBounds();
    }

    void Model::BuildBounds()
    {
        _bounds = {};
        for (const auto& mesh : _meshes)
            for (const auto& vertex : mesh.Vertices) _bounds.Include(vertex.Position);
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
