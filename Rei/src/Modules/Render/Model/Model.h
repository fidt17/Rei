#pragma once
#include "assimp/scene.h"
#include "Modules/Render/Mesh/Mesh.h"

namespace rei::render
{
    class Model
    {
    public:
        Model(resources::BinaryReader& reader);

        const std::vector<Mesh>& GetMeshes() const;
        
    private:
        std::vector<Mesh> _meshes;
        std::string _directory;

        std::vector<Texture> textures_loaded; // todo: move to some asset tracker

        std::vector<Texture> LoadMaterialTextures(aiMaterial* mat, aiTextureType type, std::string typeName);
    };
}
