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

    std::string ReadShaderInfoLog(const unsigned int shader)
    {
        int logLength = 0;
        glGetShaderiv(shader, GL_INFO_LOG_LENGTH, &logLength);
        if (logLength <= 1) return {};

        std::string log(static_cast<std::size_t>(logLength), '\0');
        GLsizei written = 0;
        glGetShaderInfoLog(shader, logLength, &written, log.data());
        if (written <= 0) return {};

        log.resize(static_cast<std::size_t>(written));
        return log;
    }

    std::string ReadProgramInfoLog(const unsigned int shaderProgram)
    {
        int logLength = 0;
        glGetProgramiv(shaderProgram, GL_INFO_LOG_LENGTH, &logLength);
        if (logLength <= 1) return {};

        std::string log(static_cast<std::size_t>(logLength), '\0');
        GLsizei written = 0;
        glGetProgramInfoLog(shaderProgram, logLength, &written, log.data());
        if (written <= 0) return {};

        log.resize(static_cast<std::size_t>(written));
        return log;
    }

    bool VerifyShaderCompilation(const unsigned int shader, const ShaderTypeEnum shaderType)
    {
        int success;
        glGetShaderiv(shader, GL_COMPILE_STATUS, &success);
        if (success != GL_TRUE)
        {
            const auto infoLog = ReadShaderInfoLog(shader);

            if (shaderType == Vertex)
            {
                LOG_ERROR("Vertex shader compilation failed\n{}", infoLog)
            }
            else if (shaderType == Fragment)
            {
                LOG_ERROR("Fragment shader compilation failed\n{}", infoLog)
            }

            return false;
        }

        return true;
    }

    bool VerifyShaderProgramLinking(const unsigned shaderProgram)
    {
        int success;
        glGetProgramiv(shaderProgram, GL_LINK_STATUS, &success);
        if (success != GL_TRUE)
        {
            const auto infoLog = ReadProgramInfoLog(shaderProgram);
            LOG_ERROR("Shader linking failed\n{}", infoLog)
            
            return false;
        }
        
        return true;
    }

    unsigned ShaderUtility::CreateShaderProgram(const char* vertexShaderSrc, const char* fragmentShaderSrc) const
    {
        const unsigned int vertexShader = CompileVertexShader(vertexShaderSrc);
        if (vertexShader == 0) return 0;

        const unsigned int fragmentShader = CompileFragmentShader(fragmentShaderSrc);
        if (fragmentShader == 0)
        {
            glDeleteShader(vertexShader);
            return 0;
        }

        const unsigned shaderProgram = glCreateProgram();
        glAttachShader(shaderProgram, vertexShader);
        glAttachShader(shaderProgram, fragmentShader);
        
        glLinkProgram(shaderProgram);
        
        glDeleteShader(vertexShader);
        glDeleteShader(fragmentShader);

        if (VerifyShaderProgramLinking(shaderProgram)) return shaderProgram;

        glDeleteProgram(shaderProgram);
        return 0;
    }

    unsigned ShaderUtility::CompileVertexShader(const char* src) const
    {
        const unsigned int vertexShader = glCreateShader(GL_VERTEX_SHADER);
        glShaderSource(vertexShader, 1, &src, nullptr);
        glCompileShader(vertexShader);

        if (VerifyShaderCompilation(vertexShader, Vertex)) return vertexShader;

        glDeleteShader(vertexShader);
        return 0;
    }

    unsigned ShaderUtility::CompileFragmentShader(const char* src) const
    {
        const unsigned int fragmentShader = glCreateShader(GL_FRAGMENT_SHADER);
        glShaderSource(fragmentShader, 1, &src, nullptr);
        glCompileShader(fragmentShader);

        if (VerifyShaderCompilation(fragmentShader, Fragment)) return fragmentShader;

        glDeleteShader(fragmentShader);
        return 0;
    }
}
