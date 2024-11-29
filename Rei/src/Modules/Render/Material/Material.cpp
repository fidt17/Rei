#include "pch.h"
#include "Material.h"

rei::render::Material::Material(const Shader& shader): _shader(shader)
{
}

rei::render::Material::~Material()
{
    _shader.Delete();
}

const rei::render::Shader& rei::render::Material::GetShader() const
{
    return _shader;
}
