#pragma once

namespace rei::render
{
    class Shader
    {
    public:
        explicit Shader(resources::BinaryReader& reader);
        Shader(const char* vertexSource, const char* fragmentSource);
        ~Shader();

        void Use() const;

        void SetFloat(const std::string& name, float value) const;
        
    private:
        u32 _id;
    };
}
