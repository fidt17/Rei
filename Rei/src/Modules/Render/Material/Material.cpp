#include "pch.h"
#include "Material.h"

rei::render::Material::Material(assets::AssetRef<Shader> shader)
    :_shader(shader)
{
}

rei::render::Material::~Material()
{
    _shader->Delete();
}

const rei::render::Shader& rei::render::Material::GetShader() const
{
    return *_shader.Asset;
}

std::vector<rei::assets::AssetRef<rei::render::Texture>>& rei::render::Material::GetTextures()
{
    return _textures;
}

