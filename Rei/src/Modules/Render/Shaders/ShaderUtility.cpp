#include "pch.h"
#include "ShaderUtility.h"

#include "glad/glad.h"

namespace rei::render
{
    enum ShaderTypeEnum
    {
        Vertex,
        Fragment
    };

    void VerifyShaderCompilation(const unsigned int shader, const ShaderTypeEnum shaderType)
    {
        int success;
        glGetShaderiv(shader, GL_COMPILE_STATUS, &success);
        if (!success)
        {
            constexpr int LOG_BUFFER_SIZE = 512;
            char infoLog[LOG_BUFFER_SIZE];
            glGetShaderInfoLog(shader, LOG_BUFFER_SIZE, nullptr, infoLog);

            if (shaderType == Vertex)
            {
                LOG_ERROR("Vertex shader compilation failed\n,{}", std::string(infoLog))
            }
            else if (shaderType == Fragment)
            {
                LOG_ERROR("Fragment shader compilation failed\n{}", std::string(infoLog))
            }
        }
    }

    void VerifyShaderProgramLinking(const unsigned shaderProgram)
    {
        int success;
        glGetProgramiv(shaderProgram, GL_LINK_STATUS, &success);
        if (!success)
        {
            constexpr int LOG_BUFFER_SIZE = 512;
            char infoLog[LOG_BUFFER_SIZE];
            glGetProgramInfoLog(shaderProgram, LOG_BUFFER_SIZE, nullptr, infoLog);
            LOG_ERROR("Shader linking failed\n{}", std::string(infoLog))
        }
    }

    unsigned ShaderUtility::CreateShaderProgram(const char* vertexShaderSrc, const char* fragmentShaderSrc) const
    {
        const unsigned int vertexShader = CompileVertexShader(vertexShaderSrc);
        const unsigned int fragmentShader = CompileFragmentShader(fragmentShaderSrc);

        const unsigned shaderProgram = glCreateProgram();
        glAttachShader(shaderProgram, vertexShader);
        glAttachShader(shaderProgram, fragmentShader);
        
        glLinkProgram(shaderProgram);
        
        glDeleteShader(vertexShader);
        glDeleteShader(fragmentShader);

        VerifyShaderProgramLinking(shaderProgram);

        return shaderProgram;
    }

    unsigned ShaderUtility::CompileVertexShader(const char* src) const
    {
        const unsigned int vertexShader = glCreateShader(GL_VERTEX_SHADER);
        glShaderSource(vertexShader, 1, &src, nullptr);
        glCompileShader(vertexShader);

        VerifyShaderCompilation(vertexShader, Vertex);

        return vertexShader;
    }

    unsigned ShaderUtility::CompileFragmentShader(const char* src) const
    {
        const unsigned int fragmentShader = glCreateShader(GL_FRAGMENT_SHADER);
        glShaderSource(fragmentShader, 1, &src, nullptr);
        glCompileShader(fragmentShader);

        VerifyShaderCompilation(fragmentShader, Fragment);

        return fragmentShader;
    }
}
