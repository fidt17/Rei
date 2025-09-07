#include "pch.h"
#include "MeshRenderer.h"

#include "glad/glad.h"
#include "Modules/Editor/SelectionCollider.h"
#include "Modules/Physics/MeshCollider.h"

namespace rei::render
{
    void MeshRenderer::RenderMesh(const std::vector<Mesh>::value_type& mesh) const
    {
        glBindVertexArray(mesh.VAO);
        glDrawElements(GL_TRIANGLES, mesh.Indices.size(), GL_UNSIGNED_INT, 0);
        glBindVertexArray(0);
    }

    void MeshRenderer::ConfigureSelectionCollider() const
    {
        ECS_WORLD(GetInternalWorld());
        
        const auto meshCollider = std::make_shared<physics::MeshCollider>();
        meshCollider->SetModel(_model);
        GET(GetEntity(), editor::SelectionCollider).Collider = meshCollider;
    }

    void MeshRenderer::BindTextures() const
    {
        if (!_material.IsLoaded) return;

        unsigned int diffuseNr = 1;
        unsigned int specularNr = 1;
        unsigned int normalNr = 1;
        unsigned int heightNr = 1;

        const std::vector<assets::AssetRef<Texture>>& textures = _material.Asset->GetTextures();
        for (unsigned int i = 0; i < textures.size(); i++)
        {
            if (!textures[i].IsLoaded)
            {
                LOG_ERROR("Texture " + textures[i].Id + " is not loaded")
                continue;
            }
            const auto texturePtr = textures[i].Asset;

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

            _material.Asset->GetShader().SetInt(textureName + number, i);
            texturePtr->Use(i);
        }
    }

    void MeshRenderer::LoadAssets(assets::AssetManager& assetManager)
    {
        assetManager.Load(_model);
        assetManager.Load(_material);
    }

    void MeshRenderer::Init()
    {
        ConfigureSelectionCollider();
    }

    void MeshRenderer::Render() const
    {
        if (!_model.IsLoaded)
        {
            LOG_ERROR("Model " + _model.Id + " is not loaded. Cannot render mesh.");
            return;
        }

        GetRenderShader().Use();
        BindTextures();

        for (const auto& mesh : _model.Asset->GetMeshes())
        {
            RenderMesh(mesh);
        }
    }

    void MeshRenderer::SetModel(const assets::AssetRef<Model>& model)
    {
        _model = model;
    }

    void MeshRenderer::SetMaterial(const assets::AssetRef<Material>& material)
    {
        _material = material;
    }

    assets::AssetRef<Model>& MeshRenderer::GetModel()
    {
        return _model;
    }

    assets::AssetRef<Material>& MeshRenderer::GetMaterial()
    {
        return _material;
    }

    const Shader& MeshRenderer::GetRenderShader() const
    {
        if (_material.IsLoaded) return _material.Asset->GetShader();

        return GetAssetManager().GetById<Material>(REI_FALLBACK_MATERIAL_ID).Asset->GetShader();
    }
}
