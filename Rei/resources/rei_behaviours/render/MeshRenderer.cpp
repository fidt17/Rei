#include "pch.h"
#include "MeshRenderer.h"

#include "glad/glad.h"

namespace rei::render
{
    void MeshRenderer::Render() const
    {
        _material->GetShader().Use();

        unsigned int diffuseNr = 1;
        unsigned int specularNr = 1;
        unsigned int normalNr = 1;
        unsigned int heightNr = 1;

        std::vector<assets::AssetRef<Texture>>& textures = _material->GetTextures();
        for (unsigned int i = 0; i < textures.size(); i++)
        {
            if (!textures[i].IsLoaded)
            {
                LOG_ERROR("Texture " + textures[i].Id + " is not loaded")
                continue;
            }
            auto texturePtr = textures[i].Asset;

            glActiveTexture(GL_TEXTURE0 + i); // activate proper texture unit before binding

            // retrieve texture number (the N in diffuse_textureN)
            std::string number;
            std::string textureName;
            switch (const TextureType textureType = texturePtr->GetType())
            {
            case Diffuse:
                number = std::to_string(diffuseNr++);
                textureName = "texture_diffuse";
                break;
            case Specular:
                number = std::to_string(specularNr++);
                textureName = "texture_specular";
                break;
            case Normal:
                number = std::to_string(normalNr++);
                textureName = "texture_normal";
                break;
            case Height:
                number = std::to_string(heightNr++);
                textureName = "texture_height";
                break;
            default:
                LOG_ERROR("Unknown texture type: " + textureType)
                continue;
            }

            _material->GetShader().SetInt(textureName + number, i);
            glBindTexture(GL_TEXTURE_2D, texturePtr->GetId());
        }
        glActiveTexture(GL_TEXTURE0);

        // draw mesh
        glBindVertexArray(_mesh.VAO);
        glDrawElements(GL_TRIANGLES, _mesh.Indices.size(), GL_UNSIGNED_INT, 0);
        glBindVertexArray(0);
    }

    void MeshRenderer::SetMesh(const Mesh& mesh)
    {
        _mesh = mesh;
    }

    void MeshRenderer::SetMaterial(const std::shared_ptr<Material>& material)
    {
        _material = material;
    }

    const Material& MeshRenderer::GetMaterial() const
    {
        return *_material;
    }

    const Shader& MeshRenderer::GetShader() const
    {
        return _material->GetShader();
    }
}
