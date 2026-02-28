#include "pch.h"
#include "Shader.h"

#include "ShaderGenerator.h"
#include "ShaderUtility.h"
#include "glad/glad.h"
#include "glm/gtc/type_ptr.hpp"

namespace rei::render
{
    SET_LOG_SCOPE("Shader")

    Shader::Shader(resources::BinaryReader& reader)
    {
        const auto content = reader.GetStr();
        _vertexSource = ShaderGenerator::GetInstance().ComposeVertexSource(content);
        _fragmentSource = ShaderGenerator::GetInstance().ComposeFragmentSource(content);
        LOG_DEBUG("Shader deserialized vertexLen={}, fragmentLen={}", _vertexSource.size(), _fragmentSource.size())
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

        LOG_DEBUG("Deleting shader: {}", _id);
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
        Use();
        glUniformMatrix4fv(GetLocation("_Projection"), 1, GL_FALSE, glm::value_ptr(projectionMatrix));
        glUniformMatrix4fv(GetLocation("_View"), 1, GL_FALSE, glm::value_ptr(viewMatrix));
        glUniformMatrix4fv(GetLocation("_Model"), 1, GL_FALSE, glm::value_ptr(modelMatrix));
    }

    void Shader::PostLoad()
    {
        LOG_DEBUG("Shader PostLoad start id={}", _id)

        if (_id == 0)
        {
            _id = ShaderUtility().CreateShaderProgram(_vertexSource.c_str(), _fragmentSource.c_str());
            LOG_DEBUG("Shader program created id={}", _id)
        }
        else
        {
            LOG_DEBUG("Shader PostLoad skipped create, already has id={}", _id)
        }
        LOG_DEBUG("Shader PostLoad complete id={}", _id)
    }

    Shader Shader::CreateInstanceFrom(const Shader& source)
    {
        LOG_DEBUG("Create shader instance from source id={}", source._id)
        Shader instance;
        instance._id = ShaderUtility().CreateShaderProgram(source._vertexSource.c_str(), source._fragmentSource.c_str());
        LOG_DEBUG("Created shader: {}", instance._id);
        
        return instance;
    }
}

