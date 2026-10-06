#include "pch.h"
#include "Common/Profiling/ProfileMarkers.h"
#include "Shader.h"

#include "ShaderGenerator.h"
#include "ShaderUtility.h"
#include "glad/glad.h"
#include "glm/gtc/type_ptr.hpp"
#include <atomic>
#include "GLFW/glfw3.h"

namespace
{
    thread_local const rei::render::Shader* uniformBatchShader = nullptr;
    thread_local const rei::render::Shader* boundShader = nullptr;
    thread_local const void* boundContext = nullptr;
}

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
          _programRevision(other._programRevision),
          _locations(std::move(other._locations)),
          _uniformNamesByType(std::move(other._uniformNamesByType)),
          _vertexSource(std::move(other._vertexSource)),
          _fragmentSource(std::move(other._fragmentSource))
    {
        other._id = 0;
        other._programRevision = 0;
        other._locations.clear();
        other._uniformNamesByType.clear();
    }

    Shader& Shader::operator=(Shader&& other) noexcept
    {
        if (this == &other)
        {
            return *this;
        }

        Delete();
        _id = other._id;
        _programRevision = other._programRevision;
        _vertexSource = std::move(other._vertexSource);
        _fragmentSource = std::move(other._fragmentSource);
        _locations = std::move(other._locations);
        _uniformNamesByType = std::move(other._uniformNamesByType);
        other._id = 0;
        other._programRevision = 0;
        other._locations.clear();
        other._uniformNamesByType.clear();

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
            static std::atomic<u64> nextProgramRevision{0};
            _programRevision = ++nextProgramRevision;
            CacheUniformNames();
        }
    }

    void Shader::Use() const
    {
        glUseProgram(_id);
        boundShader = this;
        boundContext = glfwGetCurrentContext();
        profiling::Count(profiling::markers::SHADER_USE_CALLS.Id);
    }

    Shader::UniformBatch::UniformBatch(const Shader& shader, const bool bindProgram) : _previous(uniformBatchShader)
    {
        uniformBatchShader = &shader;
        if (bindProgram) shader.Use();
    }

    Shader::UniformBatch::~UniformBatch()
    {
        uniformBatchShader = _previous;
    }

    void Shader::InvalidateProgramBinding()
    {
        boundShader = nullptr;
        boundContext = nullptr;
    }

    bool Shader::PrepareUniform(const i32 location) const
    {
        if (_id == 0 || location < 0) return false;
        const auto context = glfwGetCurrentContext();
        if (uniformBatchShader != this || boundShader != this || boundContext != context) Use();
        return true;
    }

    void Shader::Delete() const
    {
        if (_id != 0) glDeleteProgram(_id);
        _id = 0;
        _programRevision = 0;
        _locations.clear();
        if (boundShader == this) boundShader = nullptr;
        _uniformNamesByType.clear();
    }

    i32 Shader::GetLocation(const std::string& name) const
    {
        if (_id == 0) return -1;
        const auto found = _locations.find(name);
        if (found != _locations.end()) return found->second;
        const auto location = glGetUniformLocation(_id, name.c_str());
        _locations.emplace(name, location); // Cache inactive uniforms (-1) too.
        return location;
    }

    void Shader::SetInt(const std::string& name, const i32 value) const
    {
        SetInt(GetLocation(name), value);
    }

    void Shader::SetFloat(const std::string& name, const f32 value) const
    {
        SetFloat(GetLocation(name), value);
    }

    void Shader::SetVector3(const std::string& name, const math::Vector3& value) const
    {
        SetVector3(GetLocation(name), value);
    }

    void Shader::SetColor(const std::string& name, const Color& value) const
    {
        const auto location = GetLocation(name);
        if (location < 0) return;
        SetLinearColor(location, value.ToLinear());
    }

    void Shader::SetMatrix4f(const std::string& name, glm::mat4 value) const
    {
        SetMatrix4f(GetLocation(name), value);
    }

    void Shader::SetInt(const i32 location, const i32 value) const
    {
        if (!PrepareUniform(location)) return;
        glUniform1i(location, value);
        profiling::RecordUniformUpload();
    }

    void Shader::SetFloat(const i32 location, const f32 value) const
    {
        if (!PrepareUniform(location)) return;
        glUniform1f(location, value);
        profiling::RecordUniformUpload();
    }

    void Shader::SetVector3(const i32 location, const math::Vector3& value) const
    {
        if (!PrepareUniform(location)) return;
        glUniform3f(location, value.x, value.y, value.z);
        profiling::RecordUniformUpload();
    }

    void Shader::SetLinearColor(const i32 location, const Color& value) const
    {
        if (!PrepareUniform(location)) return;
        glUniform4f(location, value.r, value.g, value.b, value.a);
        profiling::RecordUniformUpload();
    }

    void Shader::SetMatrix4f(const i32 location, const glm::mat4& value) const
    {
        const auto data = value_ptr(value);
        if (!PrepareUniform(location)) return;
        glUniformMatrix4fv(location, 1, GL_FALSE, data);
        profiling::RecordUniformUpload();
    }

    void Shader::SetViewMatrices(const glm::mat4& projectionMatrix, const glm::mat4& viewMatrix, const glm::mat4& modelMatrix) const
    {
        UniformBatch uniforms(*this);
        {
            profiling::UniformPhaseScope phase(profiling::UniformPhase::Camera);
            SetMatrix4f("_Projection", projectionMatrix);
            SetMatrix4f("_View", viewMatrix);
        }
        {
            profiling::UniformPhaseScope phase(profiling::UniformPhase::Object);
            SetMatrix4f("_Model", modelMatrix);
        }
    }

    const std::vector<std::string>& Shader::GetUniformNamesByType(const u32 uniformType) const
    {
        static const std::vector<std::string> empty;
        const auto found = _uniformNamesByType.find(uniformType);
        return found == _uniformNamesByType.end() ? empty : found->second;
    }

    void Shader::CacheUniformNames()
    {
        _locations.clear();
        _uniformNamesByType.clear();
        i32 uniformCount = 0;
        i32 maxNameLength = 0;
        glGetProgramiv(_id, GL_ACTIVE_UNIFORMS, &uniformCount);
        glGetProgramiv(_id, GL_ACTIVE_UNIFORM_MAX_LENGTH, &maxNameLength);
        std::vector<GLchar> uniformNameBuffer(std::max(maxNameLength, 1));
        for (i32 i = 0; i < uniformCount; i++)
        {
            GLsizei uniformNameLength = 0;
            i32 uniformSize = 0;
            GLenum activeUniformType = 0;
            glGetActiveUniform(
                _id,
                static_cast<u32>(i),
                static_cast<i32>(uniformNameBuffer.size()),
                &uniformNameLength,
                &uniformSize,
                &activeUniformType,
                uniformNameBuffer.data());

            if (uniformNameLength <= 0) continue;

            auto uniformName = std::string(uniformNameBuffer.data(), uniformNameLength);
            const auto arraySuffixPosition = uniformName.find("[0]");
            if (arraySuffixPosition != std::string::npos)
            {
                uniformName = uniformName.substr(0, arraySuffixPosition);
            }

            _uniformNamesByType[activeUniformType].push_back(std::move(uniformName));
        }
    }

    Shader Shader::CreateInstanceFrom(const Shader& source)
    {
        Shader instance;
        instance._vertexSource = source._vertexSource;
        instance._fragmentSource = source._fragmentSource;
        instance.PostLoad();
        
        return instance;
    }
}

