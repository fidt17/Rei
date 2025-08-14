#include "pch.h"
#include "Shader.h"

#include "ShaderUtility.h"
#include "glad/glad.h"
#include "glm/gtc/type_ptr.hpp"

namespace rei::render
{
    Shader::Shader(resources::BinaryReader& reader)
    {
        const auto content = reader.GetStr();

        const std::string version = "#version 330 core\n";
        const std::string vertexShader = version + "#define VERTEX;\n" + content;
        const std::string fragmentShader = version + "#define FRAGMENT;\n" + content;

        _id = ShaderUtility().CreateShaderProgram(vertexShader.c_str(), fragmentShader.c_str());

#if DEBUG
        _vertexShader = vertexShader;
        _fragmentShader = fragmentShader;
#endif
    }

    Shader::Shader(const char* vertexSource, const char* fragmentSource)
    {
        _id = ShaderUtility().CreateShaderProgram(vertexSource, fragmentSource);
    }

    void Shader::Use() const
    {
        glUseProgram(_id);
    }

    void Shader::Delete() const
    {
        glDeleteProgram(_id);
    }

    i32 Shader::GetLocation(const std::string& name) const
    {
        return glGetUniformLocation(_id, name.c_str());
    }

    void Shader::SetInt(const std::string& name, int value) const
    {
        Use();
        glUniform1i(GetLocation(name), value);
    }

    void Shader::SetFloat(const std::string& name, const float value) const
    {
        Use();
        glUniform1f(GetLocation(name), value);
    }

    void Shader::SetVector3(const std::string& name, const math::Vector3& value) const
    {
        Use();
        glUniform3f(GetLocation(name), value.x, value.y, value.z);
    }

    void Shader::SetColor(const std::string& name, const Color& value) const
    {
        Use();
        glUniform4f(GetLocation(name), value.r, value.g, value.b, value.a);
    }

    void Shader::SetMatrix4f(const std::string& name, glm::mat4 value) const
    {
        Use();
        glUniformMatrix4fv(GetLocation(name), 1, GL_FALSE, value_ptr(value));
    }

    void Shader::SetViewMatrices(const glm::mat4& projectionMatrix, const glm::mat4& viewMatrix, const glm::mat4& modelMatrix) const
    {
        glUniformMatrix4fv(GetLocation("projection"), 1, GL_FALSE, glm::value_ptr(projectionMatrix));
        glUniformMatrix4fv(GetLocation("view"), 1, GL_FALSE, glm::value_ptr(viewMatrix));
        glUniformMatrix4fv(GetLocation("model"), 1, GL_FALSE, glm::value_ptr(modelMatrix));
    }
}
