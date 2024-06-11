#include "pch.h"
#include "Shader.h"

#include "ShaderUtility.h"
#include "glad/glad.h"

namespace rei::render
{
    Shader::Shader(const char* vertexSource, const char* fragmentSource)
    {
        _id = ShaderUtility().CreateShaderProgram(vertexSource, fragmentSource);
    }

    Shader::~Shader()
    {
        glDeleteShader(_id);
    }

    void Shader::Use() const
    {
        glUseProgram(_id);
    }

    void Shader::SetFloat(const std::string& name, const float value) const
    {
        glUniform1f(glGetUniformLocation(_id, name.c_str()), value);
    }
}
