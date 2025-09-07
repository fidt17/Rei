#include "pch.h"
#include "ModelBuilder.h"

#include "assimp/Importer.hpp"
#include "assimp/postprocess.h"

rei::render::Mesh ModelBuilder::ProcessMesh(const aiMesh* mesh) const
{
    std::vector<rei::render::Vertex> vertices;
    std::vector<unsigned int> indices;
    std::vector<rei::render::Face> faces;

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
        const aiFace ai_face = mesh->mFaces[i];
        auto& face = faces.emplace_back();
        for (unsigned int j = 0; j < ai_face.mNumIndices; j++)
        {
            auto idx = ai_face.mIndices[j];
            indices.push_back(idx);
            face.Vertices.push_back(vertices[idx]);
        }
    }

    return rei::render::Mesh("NaN", vertices, indices, faces);
}

void ModelBuilder::ProcessNode(const aiNode* node, const aiScene* scene, std::vector<rei::render::Mesh>& meshes) const
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

void ModelBuilder::BuildModelAsset(const std::filesystem::path& assetPath, rei::resources::BinaryWriter& writer) const
{
    Assimp::Importer importer;
    const aiScene* scene = importer.ReadFile(assetPath.string(), aiProcess_Triangulate | aiProcess_FlipUVs | aiProcess_GenNormals);

    std::vector<rei::render::Mesh> meshes;
    ProcessNode(scene->mRootNode, scene, meshes);

    writer.WriteStr(assetPath.filename().generic_string());

    writer.WriteI32(meshes.size());
    i32 meshCounter = 0;
    for (const auto& mesh : meshes)
    {
        writer.WriteStr(assetPath.filename().generic_string() + ":" + STRING(meshCounter++));

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

        // faces
        writer.WriteI32(mesh.Faces.size());
        for (auto& face : mesh.Faces)
        {
            writer.WriteI32(face.Vertices.size());
            for (int i = 0; i < face.Vertices.size(); i++)
            {
                writer.Write(face.Vertices[i]);
            }
        }
    }
}
