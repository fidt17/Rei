#include "pch.h"
#include "MeshBuilder.h"

#include "assimp/Importer.hpp"
#include "assimp/postprocess.h"

rei::render::Mesh MeshBuilder::ProcessMesh(const aiMesh* mesh) const
{
    std::vector<rei::render::Vertex> vertices;
    std::vector<unsigned int> indices;

    // process vertices
    for (unsigned int i = 0; i < mesh->mNumVertices; i++)
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
    for (unsigned int i = 0; i < mesh->mNumFaces; i++)
    {
        const aiFace face = mesh->mFaces[i];
        for (unsigned int j = 0; j < face.mNumIndices; j++)
        {
            indices.push_back(face.mIndices[j]);
        }
    }

    return rei::render::Mesh(vertices, indices, std::vector<rei::render::Texture>());
}

void MeshBuilder::ProcessNode(const aiNode* node, const aiScene* scene, std::vector<rei::render::Mesh>& meshes) const
{
    // process node's meshes
    for (unsigned int i = 0; i < node->mNumMeshes; i++)
    {
        const aiMesh* mesh = scene->mMeshes[node->mMeshes[i]];
        meshes.push_back(ProcessMesh(mesh));
    }

    // recursive call to sub-nodes
    for (unsigned int i = 0; i < node->mNumChildren; i++)
    {
        ProcessNode(node->mChildren[i], scene, meshes);
    }
}

void MeshBuilder::BuildMeshAsset(const std::filesystem::path& assetPath, rei::resources::BinaryWriter& writer) const
{
    Assimp::Importer importer;
    const aiScene* scene = importer.ReadFile(assetPath.string(), aiProcess_Triangulate | aiProcess_FlipUVs | aiProcess_GenNormals);

    std::vector<rei::render::Mesh> meshes;
    ProcessNode(scene->mRootNode, scene, meshes);

    // meshes
    writer.WriteI32(meshes.size());
    for (auto mesh : meshes)
    {
        // vertices
        writer.WriteI32(mesh.Vertices.size());
        for (const auto& vertex : mesh.Vertices)
        {
            writer.Write(vertex);
        }

        // indices
        writer.WriteI32(mesh.Indices.size());
        for (const auto& index : mesh.Indices)
        {
            writer.Write(index);
        }
    }
}
