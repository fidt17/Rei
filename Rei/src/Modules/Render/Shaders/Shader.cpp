#include "pch.h"
#include "Shader.h"

#include "ShaderUtility.h"
#include "glad/glad.h"

namespace rei::render
{
    Shader::Shader(resources::BinaryReader& reader)
    {
        const auto content = reader.GetStr();

        const std::string version = "#version 330 core\n";
        const std::string vertexShader = version + "#define VERTEX;\n" + content;
        const std::string fragmentShader = version + "#define FRAGMENT;\n" + content;

        _id = ShaderUtility().CreateShaderProgram(vertexShader.c_str(), fragmentShader.c_str());
    }

    Shader::Shader(const char* vertexSource, const char* fragmentSource)
    {
        _id = ShaderUtility().CreateShaderProgram(vertexSource, fragmentSource);
    }

    Shader::~Shader()
    {
        glDeleteProgram(_id);
    }

    void Shader::Use() const
    {
        glUseProgram(_id);
    }

    i32 Shader::GetLocation(const std::string& name) const
    {
        return glGetUniformLocation(_id, name.c_str());
    }

    void Shader::SetInt(const std::string& name, int value) const
    {
        glUniform1i(glGetUniformLocation(_id, name.c_str()), value);
    }

    void Shader::SetFloat(const std::string& name, const float value) const
    {
        glUniform1f(glGetUniformLocation(_id, name.c_str()), value);
    }
}
