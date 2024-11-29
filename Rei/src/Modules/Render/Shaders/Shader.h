#pragma once
#include "glm/fwd.hpp"
#include "Modules/Render/Color/Color.h"

namespace rei::render
{
    class Shader
    {
    public:

        REI_API Shader() = default;
        explicit Shader(resources::BinaryReader& reader);
        Shader(const char* vertexSource, const char* fragmentSource);

        void Use() const;
        void Delete() const;

        REI_API i32 GetLocation(const std::string& name) const;
        REI_API void SetInt(const std::string& name, int value) const;
        REI_API void SetFloat(const std::string& name, float value) const;
        REI_API void SetVector3(const std::string& name, const math::Vector3& value) const;
        REI_API void SetColor(const std::string& name, const Color& value) const;
        REI_API void SetMatrix4f(const std::string& name, glm::mat4 value) const;
        
    private:
        u32 _id;
        
#if DEBUG
        std::string _vertexShader;
        std::string _fragmentShader;
#endif
    };
}
