#include "pch.h"
#include "Model.h"
#include "Common/Time/Stopwatch.h"
#include "Common/Profiling/ProfilingService.h"

namespace rei::render
{

    Model::Model(resources::BinaryReader& reader)
    {
        time::Stopwatch timer;
        timer.Start();
        _name = reader.GetStr();

        const i32 meshCount = reader.GetI32();
        REI_THROW_IF(meshCount < 0, "Negative packed model mesh count")

        for (i32 i = 0; i < meshCount; i++)
        {
            _meshes.emplace_back(reader);
        }
        const auto meshesMs = timer.ElapsedMs();
        BuildBounds();
        const auto totalMs = timer.ElapsedMs();
        if (profiling::IsLoadTimingEnabled()) profiling::WriteLoadTiming(nlohmann::json({
            {"stage", "model.read"}, {"name", _name}, {"meshes", _meshes.size()},
            {"meshesMs", meshesMs}, {"boundsMs", totalMs - meshesMs}, {"totalMs", totalMs}
        }).dump());
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
        time::Stopwatch timer;
        timer.Start();
        for (auto& value : _meshes)
        {
            value.Dispose();
        }
        const auto gpuMs = timer.ElapsedMs();
        // Clear here to measure CPU destruction separately from the OpenGL calls above.
        _meshes.clear();
        const auto totalMs = timer.ElapsedMs();
        if (profiling::IsLoadTimingEnabled()) profiling::WriteLoadTiming(nlohmann::json({
            {"stage", "model.release"}, {"name", _name}, {"gpuMs", gpuMs}, {"cpuMs", totalMs - gpuMs}, {"totalMs", totalMs}
        }).dump());
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
