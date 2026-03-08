#pragma once
#include "Modules/Render/Color/Color.h"

namespace rei::render
{
    class Shader
    {
    public:

        REI_API Shader() = default;
        REI_API explicit Shader(resources::BinaryReader& reader);
        Shader(const Shader& other) = delete;
        Shader& operator=(const Shader& other) = delete;
        REI_API Shader(Shader&& other) noexcept;
        REI_API Shader& operator=(Shader&& other) noexcept;
        REI_API ~Shader();

        REI_API void Use() const;
        REI_API void Delete() const;

        REI_API i32 GetLocation(const std::string& name) const;
        REI_API void SetInt(const std::string& name, i32 value) const;
        REI_API void SetFloat(const std::string& name, f32 value) const;
        REI_API void SetVector3(const std::string& name, const math::Vector3& value) const;
        REI_API void SetColor(const std::string& name, const Color& value) const;
        REI_API void SetMatrix4f(const std::string& name, glm::mat4 value) const;

        REI_API void SetViewMatrices(const glm::mat4& projectionMatrix, const glm::mat4& viewMatrix, const glm::mat4& modelMatrix) const;
        REI_API void PostLoad();
        
        static REI_API Shader CreateInstanceFrom(const Shader& source);
        
    private:
        u32 _id = 0;

        std::string _vertexSource;
        std::string _fragmentSource;
    };
}
