#include "pch.h"
#include "Shader.h"

#include "ShaderUtility.h"
#include "glad/glad.h"
#include "glm/gtc/type_ptr.hpp"
#include "Modules/Assets/TextAsset.h"

namespace rei::render
{
    std::string shader_includes = "NaN";
    std::string shader_vertex_includes = "NaN";
    std::string shader_fragment_includes = "NaN";

    void GenerateShaderIncludes()
    {
        if (shader_includes != "NaN") return;

        shader_includes = "\n// --- SHADER INCLUDES START ---\n";

        shader_includes += "\n#define NR_POINT_LIGHTS " + STRING(REI_MAX_POINT_LIGHTS_COUNT);

        std::vector<std::string> includes{};
        includes.emplace_back(REI_SHADER_INCLUDE_AMBIENT_LIGHT_ASSET_ID);
        includes.emplace_back(REI_SHADER_INCLUDE_POINT_LIGHT_ASSET_ID);
        includes.emplace_back(REI_SHADER_INCLUDE_SHADER_COMMON_ASSET_ID);

        shader_includes += "\n";
        for (const auto& include : includes)
        {
            const auto i = GetAssetManager().GetById<assets::TextAsset>(include);
            shader_includes += "\n" + i.Asset->GetValue();
        }
        shader_includes += "\n// --- SHADER INCLUDES END ---\n";
    }

    void GenerateShaderVertexIncludes()
    {
        if (shader_vertex_includes != "NaN") return;

        shader_vertex_includes = "\n// --- SHADER VERTEX INCLUDES START ---\n";

        std::vector<std::string> includes{};
        includes.emplace_back(REI_SHADER_INCLUDE_VERTEX_COMMON_ASSET_ID);

        for (const auto& include : includes)
        {
            const auto i = GetAssetManager().GetById<assets::TextAsset>(include);
            shader_vertex_includes += "\n" + i.Asset->GetValue();
        }
        shader_vertex_includes += "\n// --- SHADER VERTEX INCLUDES END ---\n";
    }
    
    void GenerateShaderFragmentIncludes()
    {
        if (shader_fragment_includes != "NaN") return;

        shader_fragment_includes = "\n// --- SHADER FRAGMENT INCLUDES START ---\n";

        std::vector<std::string> includes{};
        includes.emplace_back(REI_SHADER_INCLUDE_FRAGMENT_COMMON_ASSET_ID);

        for (const auto& include : includes)
        {
            const auto i = GetAssetManager().GetById<assets::TextAsset>(include);
            shader_fragment_includes += "\n" + i.Asset->GetValue();
        }
        shader_fragment_includes += "\n// --- SHADER FRAGMENT INCLUDES END ---\n";
    }

    Shader::Shader(resources::BinaryReader& reader)
    {
        GenerateShaderIncludes();
        GenerateShaderVertexIncludes();
        GenerateShaderFragmentIncludes();

        const auto content = reader.GetStr();

        const std::string version = "#version 330 core\n";

        _vertexSource = version + shader_includes + shader_vertex_includes + "\n#define VERTEX;\n" + content;
        _fragmentSource = version + shader_includes + shader_fragment_includes + "\n#define FRAGMENT;\n" + content;
        
        _id = ShaderUtility().CreateShaderProgram(_vertexSource.c_str(), _fragmentSource.c_str());
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
        Use();
        glUniformMatrix4fv(GetLocation("_Projection"), 1, GL_FALSE, glm::value_ptr(projectionMatrix));
        glUniformMatrix4fv(GetLocation("_View"), 1, GL_FALSE, glm::value_ptr(viewMatrix));
        glUniformMatrix4fv(GetLocation("_Model"), 1, GL_FALSE, glm::value_ptr(modelMatrix));
    }

    Shader Shader::CreateInstanceFrom(const Shader& source)
    {
        Shader instance;
        instance._id = ShaderUtility().CreateShaderProgram(source._vertexSource.c_str(), source._fragmentSource.c_str());
        
        return instance;
    }
}
