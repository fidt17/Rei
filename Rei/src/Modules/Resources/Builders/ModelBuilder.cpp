#include "pch.h"
#include "ModelBuilder.h"
#include "Common/Math/Bounds.h"

#include "assimp/Importer.hpp"
#include "assimp/postprocess.h"

namespace
{
    void WriteBVHNode(const rei::render::MeshBVHNode& node, rei::resources::BinaryWriter& writer)
    {
        writer.Write(node.Min);
        writer.Write(node.Max);
        writer.WriteI32(static_cast<i32>(node.Triangles.size()));
        for (const auto triangle : node.Triangles) writer.WriteU32(triangle);

        const bool hasLeft = node.Left != nullptr;
        writer.WriteU8(hasLeft ? 1 : 0);
        if (hasLeft)
        {
            WriteBVHNode(*node.Left, writer);
        }

        const bool hasRight = node.Right != nullptr;
        writer.WriteU8(hasRight ? 1 : 0);
        if (hasRight)
        {
            WriteBVHNode(*node.Right, writer);
        }
    }
}

rei::render::Mesh ModelBuilder::ProcessMesh(const aiMesh* mesh) const
{
    std::vector<rei::render::Vertex> vertices;
    std::vector<u32> indices;

    // process vertices
    for (u32 i = 0; i < mesh->mNumVertices; i++)
    {
        rei::render::Vertex vertex;
        vertex.Position = glm::vec3(mesh->mVertices[i].x, mesh->mVertices[i].y, mesh->mVertices[i].z);
        vertex.Normal = glm::vec3(mesh->mNormals[i].x, mesh->mNormals[i].y, mesh->mNormals[i].z);

        if (mesh->mTextureCoords[0])
        {
            vertex.TexCoords = glm::vec2(mesh->mTextureCoords[0][i].x, mesh->mTextureCoords[0][i].y);
        }
        else
        {
            vertex.TexCoords = glm::vec2(0.0f, 0.0f);
        }

        vertices.push_back(vertex);
    }

    // process indices
    for (u32 i = 0; i < mesh->mNumFaces; i++)
    {
        const aiFace ai_face = mesh->mFaces[i];
        REI_THROW_IF(ai_face.mNumIndices != 3, "Imported mesh face must contain 3 vertices")
        for (u32 j = 0; j < ai_face.mNumIndices; j++)
        {
            auto idx = ai_face.mIndices[j];
            indices.push_back(idx);
        }
    }

    return rei::render::Mesh("NaN", vertices, indices);
}

void ModelBuilder::ProcessNode(const aiNode* node, const aiScene* scene, std::vector<rei::render::Mesh>& meshes) const
{
    // process node's meshes
    for (u32 i = 0; i < node->mNumMeshes; i++)
    {
        const aiMesh* mesh = scene->mMeshes[node->mMeshes[i]];
        meshes.push_back(ProcessMesh(mesh));
    }

    // recursive call to sub-nodes
    for (u32 i = 0; i < node->mNumChildren; i++)
    {
        ProcessNode(node->mChildren[i], scene, meshes);
    }
}

void ModelBuilder::BuildModelAsset(const std::filesystem::path& assetPath, rei::resources::BinaryWriter& writer) const
{
    Assimp::Importer importer;
    const aiScene* scene = importer.ReadFile(assetPath.string(), aiProcess_Triangulate | aiProcess_FlipUVs | aiProcess_GenNormals);

    std::vector<rei::render::Mesh> meshes;
    ProcessNode(scene->mRootNode, scene, meshes);

    writer.WriteStr(assetPath.filename().generic_string());

    writer.WriteI32(static_cast<i32>(meshes.size()));
    rei::math::Bounds bounds;
    i32 meshCounter = 0;
    for (auto& mesh : meshes)
    {
        writer.WriteStr(assetPath.filename().generic_string() + ":" + STRING(meshCounter++));

        // vertices
        writer.WriteI32(static_cast<i32>(mesh.Vertices.size()));
        for (const auto& vertex : mesh.Vertices)
        {
            bounds.Include(vertex.Position);
            writer.Write(vertex);
        }

        // indices
        writer.WriteI32(static_cast<i32>(mesh.Indices.size()));
        for (const auto& index : mesh.Indices)
        {
            writer.Write(index);
        }

        mesh.BVHRoot.BuildBVH(mesh);
        WriteBVHNode(mesh.BVHRoot, writer);
    }

    writer.WriteU8(bounds.IsValid() ? 1 : 0);
    if (bounds.IsValid())
    {
        writer.Write(bounds.Min);
        writer.Write(bounds.Max);
    }
}
