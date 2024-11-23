#pragma once
#include "../BaseRenderScenario.h"
#include "glad/glad.h"
#include "glfw/glfw3.h"
#include "Modules/Render/Shaders/ShaderUtility.h"

class hello_triangle_indexed final : public BaseRenderScenario
{
public:
    explicit hello_triangle_indexed(GLFWwindow* target)
        : BaseRenderScenario(target), _shaderProgram(0), _vertexBuffer(0), _vertexArray(0), _elementBuffer(0)
    {
    }

    void Dispose() override
    {
        glDeleteVertexArrays(1, &_vertexArray);
        glDeleteBuffers(1, &_vertexBuffer);
        glDeleteBuffers(1, &_elementBuffer);
        glDeleteProgram(_shaderProgram);
    }

    void ConfigureVertexData()
    {
        glGenVertexArrays(1, &_vertexArray);
        glGenBuffers(1, &_vertexBuffer);
        glGenBuffers(1, &_elementBuffer);

        glBindVertexArray(_vertexArray);

        glBindBuffer(GL_ARRAY_BUFFER, _vertexBuffer);
        float vertices[] = {
            0.5f, 0.5f, 0.0f, // top right
            0.5f, -0.5f, 0.0f, // bottom right
            -0.5f, -0.5f, 0.0f, // bottom left
            -0.5f, 0.5f, 0.0f // top left
        };
        glBufferData(GL_ARRAY_BUFFER, sizeof(vertices), vertices, GL_STATIC_DRAW);

        glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, _elementBuffer);
        unsigned int indices[] = {
            // note that we start from 0!
            0, 1, 3, // first triangle
            1, 2, 3 // second triangle
        };
        glBufferData(GL_ELEMENT_ARRAY_BUFFER, sizeof(indices), indices,GL_STATIC_DRAW);

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
        glPolygonMode(GL_FRONT_AND_BACK, GL_LINE);
    }

    void Render() override
    {
        glClearColor(19 / 255.0f, 23 / 255.0f, 30 / 255.0f, 1);
        glClear(GL_COLOR_BUFFER_BIT);

        glUseProgram(_shaderProgram);
        glBindVertexArray(_vertexArray);
        glDrawElements(GL_TRIANGLES, 6, GL_UNSIGNED_INT, 0);
        glBindVertexArray(0);

        glfwSwapBuffers(_target);
    }

private:
    unsigned int _shaderProgram;
    unsigned int _vertexBuffer, _vertexArray, _elementBuffer;

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
