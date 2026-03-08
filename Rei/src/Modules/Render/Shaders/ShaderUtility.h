#pragma once

namespace rei::render
{
    class ShaderUtility
    {
    public:
        u32 CreateShaderProgram(const char* vertexShaderSrc, const char* fragmentShaderSrc) const;
        u32 CompileVertexShader(const char* src) const;
        u32 CompileFragmentShader(const char* src) const;
    };
}
