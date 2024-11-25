#pragma once
#include "assimp/scene.h"
#include "Modules/Render/Mesh/Mesh.h"
#include "Modules/Render/Shaders/Shader.h"

namespace rei::render
{
    class Model
    {
    public:
        Model(const char* path)
        {
            LoadModel(path);
        }

        Model(resources::BinaryReader& reader);

        void Draw(const Shader& shader) const;

    private:
        std::vector<Mesh> _meshes;
        std::string _directory;

        std::vector<Texture> textures_loaded; // todo: move to some asset tracker

        void LoadModel(const std::string& path);
        void ProcessNode(const aiNode* node, const aiScene* scene);
        Mesh ProcessMesh(aiMesh* mesh, const aiScene* scene);
        std::vector<Texture> LoadMaterialTextures(aiMaterial* mat, aiTextureType type, std::string typeName);
    };
}
