#pragma once
#include "glm/fwd.hpp"

namespace rei::render
{
    class Shader
    {
    public:
        explicit Shader(resources::BinaryReader& reader);
        Shader(const char* vertexSource, const char* fragmentSource);
        ~Shader();

        void Use() const;

        i32 GetLocation(const std::string& name) const;
        void SetInt(const std::string& name, int value) const;
        void SetFloat(const std::string& name, float value) const;
        void SetMatrix4f(const std::string& name, glm::mat4 value) const;
        
    private:
        u32 _id;
    };
}
