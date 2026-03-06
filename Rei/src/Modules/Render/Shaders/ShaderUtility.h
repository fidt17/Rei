#pragma once

namespace rei::render
{
    class ShaderUtility
    {
    public:
        unsigned int CreateShaderProgram(const char* vertexShaderSrc, const char* fragmentShaderSrc) const;
        unsigned int CompileVertexShader(const char* src) const;
        unsigned int CompileFragmentShader(const char* src) const;
    };
}
