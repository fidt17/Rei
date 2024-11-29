#include "pch.h"
#include "Model.h"

#include "assimp/Importer.hpp"

namespace rei::render
{
    Model::Model(resources::BinaryReader& reader)
    {
        const i32 meshCount = reader.GetI32();
        for (int i = 0; i < meshCount; i++)
        {
            Mesh mesh(reader);
            _meshes.push_back(mesh);
        }
    }

    const std::vector<Mesh>& Model::GetMeshes() const
    {
        return _meshes;
    }

    int textureCounter;

    std::vector<Texture> Model::LoadMaterialTextures(aiMaterial* mat, aiTextureType type, std::string typeName)
    {
        std::vector<Texture> textures;
        for (unsigned int i = 0; i < mat->GetTextureCount(type); i++)
        {
            aiString str;
            mat->GetTexture(type, i, &str);
            
            auto path = std::string(str.C_Str());
            path = _directory + '/' + path;
            
            bool skip = false;
            for (unsigned int j = 0; j < textures_loaded.size(); j++)
            {
                if (textures_loaded[j].GetTag() == path)
                {
                    textures.push_back(textures_loaded[j]);
                    skip = true;
                    break;
                }
            }
            if (!skip)
            {
                // if texture hasn't been loaded already, load it
                Texture texture(path.c_str(), typeName);
                textures.push_back(texture);
                textures_loaded.push_back(texture); // add to loaded textures
            }
        }
        return textures;
    }
}
