#pragma once
#include "glad/glad.h"
#include "Modules/Render/RenderScenario/BaseRenderScenario.h"
#include "Modules/Render/Shaders/ShaderUtility.h"

class hello_triangle final : public rei::render::BaseRenderScenario
{
public:
    explicit hello_triangle(GLFWwindow* target)
        : BaseRenderScenario(target), _shaderProgram(0), _vertexBuffer(0), _vertexAttributes(0)
    {
    }

    void Dispose() override
    {
        glDeleteVertexArrays(1, &_vertexAttributes);
        glDeleteBuffers(1, &_vertexBuffer);
        glDeleteProgram(_shaderProgram);
    }

    void ConfigureVertexData()
    {
        glGenVertexArrays(1, &_vertexAttributes);
        glBindVertexArray(_vertexAttributes);
        
        glGenBuffers(1, &_vertexBuffer);
        glBindBuffer(GL_ARRAY_BUFFER, _vertexBuffer);
        
        float vertices[] = {
            -0.5f, -0.5f, 0.0f, // left  
            0.5f, -0.5f, 0.0f, // right 
            0.0f, 0.5f, 0.0f // top   
        };
        glBufferData(GL_ARRAY_BUFFER, sizeof(vertices), vertices, GL_STATIC_DRAW);
        glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, 3 * sizeof(float), (void*)0);
        glEnableVertexAttribArray(0);

        glBindBuffer(GL_ARRAY_BUFFER, 0);
        glBindVertexArray(0);
    }

    void Setup() override
    {
        _shaderProgram = rei::render::ShaderUtility().CreateShaderProgram(vertexShaderSource, fragmentShaderSource);
        ConfigureVertexData();

        // uncomment this call to draw in wireframe polygons.
        //glPolygonMode(GL_FRONT_AND_BACK, GL_LINE);
    }

    void Render() override
    {
        glClearColor(19 / 255.0f, 23 / 255.0f, 30 / 255.0f, 1);
        glClear(GL_COLOR_BUFFER_BIT);

        glUseProgram(_shaderProgram);
        glBindVertexArray(_vertexAttributes);
        
        glDrawArrays(GL_TRIANGLES, 0, 3);
        
        glBindVertexArray(0);

        glfwSwapBuffers(_target);
    }

private:
    unsigned int _shaderProgram;
    unsigned int _vertexBuffer, _vertexAttributes;

    const char* vertexShaderSource = "#version 330 core\n"
        "layout (location = 0) in vec3 aPos;\n"
        "void main()\n"
        "{\n"
        "   gl_Position = vec4(aPos.x, aPos.y, aPos.z, 1.0);\n"
        "}\0";

    const char* fragmentShaderSource = "#version 330 core\n"
        "out vec4 FragColor;\n"
        "void main()\n"
        "{\n"
        "   FragColor = vec4(1.0f, 1.0f, 1.0f, 1.0f);\n"
        "}\n\0";
};
