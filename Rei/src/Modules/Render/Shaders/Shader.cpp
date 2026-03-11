#include "pch.h"
#include "Shader.h"

#include "ShaderGenerator.h"
#include "ShaderUtility.h"
#include "glad/glad.h"
#include "glm/gtc/type_ptr.hpp"

namespace rei::render
{

    Shader::Shader(resources::BinaryReader& reader)
    {
        const auto content = reader.GetStr();
        _vertexSource = ShaderGenerator::GetInstance().ComposeVertexSource(content);
        _fragmentSource = ShaderGenerator::GetInstance().ComposeFragmentSource(content);
    }

    Shader::Shader(Shader&& other) noexcept
        : _id(other._id),
          _vertexSource(std::move(other._vertexSource)),
          _fragmentSource(std::move(other._fragmentSource))
    {
        other._id = 0;
    }

    Shader& Shader::operator=(Shader&& other) noexcept
    {
        if (this == &other)
        {
            return *this;
        }

        Delete();
        _id = other._id;
        _vertexSource = std::move(other._vertexSource);
        _fragmentSource = std::move(other._fragmentSource);
        other._id = 0;

        return *this;
    }

    Shader::~Shader()
    {
        Delete();
    }

    void Shader::PostLoad()
    {
        if (_id == 0)
        {
            _id = ShaderUtility().CreateShaderProgram(_vertexSource.c_str(), _fragmentSource.c_str());
            if (_id == 0) throw std::runtime_error("Failed to create shader program");
        }
    }

    void Shader::Use() const
    {
        glUseProgram(_id);
    }

    void Shader::Delete() const
    {
        if (_id == 0)
        {
            return;
        }

        glDeleteProgram(_id);
    }

    i32 Shader::GetLocation(const std::string& name) const
    {
        return glGetUniformLocation(_id, name.c_str());
    }

    void Shader::SetInt(const std::string& name, i32 value) const
    {
        Use();
        glUniform1i(GetLocation(name), value);
    }

    void Shader::SetFloat(const std::string& name, const f32 value) const
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
        Use();
        glUniformMatrix4fv(GetLocation("_Projection"), 1, GL_FALSE, glm::value_ptr(projectionMatrix));
        glUniformMatrix4fv(GetLocation("_View"), 1, GL_FALSE, glm::value_ptr(viewMatrix));
        glUniformMatrix4fv(GetLocation("_Model"), 1, GL_FALSE, glm::value_ptr(modelMatrix));
    }

    std::vector<std::string> Shader::GetUniformNamesByType(const u32 uniformType) const
    {
        std::vector<std::string> uniformNames;
        if (_id == 0) return uniformNames;

        i32 uniformCount = 0;
        glGetProgramiv(_id, GL_ACTIVE_UNIFORMS, &uniformCount);
        for (i32 i = 0; i < uniformCount; i++)
        {
            constexpr i32 MAX_UNIFORM_NAME_LENGTH = 256;
            GLchar uniformNameBuffer[MAX_UNIFORM_NAME_LENGTH];
            GLsizei uniformNameLength = 0;
            i32 uniformSize = 0;
            GLenum activeUniformType = 0;
            glGetActiveUniform(
                _id,
                static_cast<u32>(i),
                MAX_UNIFORM_NAME_LENGTH,
                &uniformNameLength,
                &uniformSize,
                &activeUniformType,
                uniformNameBuffer);

            if (activeUniformType != uniformType || uniformNameLength <= 0) continue;

            auto uniformName = std::string(uniformNameBuffer, uniformNameLength);
            const auto arraySuffixPosition = uniformName.find("[0]");
            if (arraySuffixPosition != std::string::npos)
            {
                uniformName = uniformName.substr(0, arraySuffixPosition);
            }

            uniformNames.push_back(uniformName);
        }

        return uniformNames;
    }

    Shader Shader::CreateInstanceFrom(const Shader& source)
    {
        Shader instance;
        instance._id = ShaderUtility().CreateShaderProgram(source._vertexSource.c_str(), source._fragmentSource.c_str());
        
        return instance;
    }
}

