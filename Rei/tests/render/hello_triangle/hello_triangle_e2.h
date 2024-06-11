#pragma once
#include "../BaseRenderScenario.h"
#include "glad/glad.h"
#include "glfw/glfw3.h"
#include "Modules/Render/Shaders/ShaderUtility.h"

class hello_triangle_e2 final : public BaseRenderScenario
{
public:
    explicit hello_triangle_e2(GLFWwindow* target)
        : BaseRenderScenario(target), _shaderProgram(0)
    {
    }

    template <size_t N>
    u32 CreateTriangle(std::array<float, N> vertices)
    {
        u32 triangle, buffer;
        glGenVertexArrays(1, &triangle);
        glBindVertexArray(triangle);

        glGenBuffers(1, &buffer);
        glBindBuffer(GL_ARRAY_BUFFER, buffer);

        glBufferData(GL_ARRAY_BUFFER, sizeof(vertices), &vertices, GL_STATIC_DRAW);
        glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, 3 * sizeof(float), (void*)0);
        glEnableVertexAttribArray(0);

        glBindBuffer(GL_ARRAY_BUFFER, 0);
        glBindVertexArray(0);

        return triangle;
    }

    void ConfigureVertexData()
    {
        constexpr std::array<float, 9> firstTriangleVertices = {
            -0.9f, -0.5f, 0.0f, // left 
            -0.0f, -0.5f, 0.0f, // right
            -0.45f, 0.5f, 0.0f, // top 
        };

        constexpr std::array<float, 9> secondTriangleVertices = {
            0.0f, -0.5f, 0.0f, // left
            0.9f, -0.5f, 0.0f, // right
            0.45f, 0.5f, 0.0f // top 
        };

        _firstTriangle = CreateTriangle(firstTriangleVertices);
        _secondTriangle = CreateTriangle(secondTriangleVertices);
    }

    void Setup() override
    {
        _shaderProgram = rei::render::ShaderUtility().CreateShaderProgram(vertexShaderSource, fragmentShaderSource);
        ConfigureVertexData();

        // uncomment this call to draw in wireframe polygons.
        glPolygonMode(GL_FRONT_AND_BACK, GL_LINE);
    }

    int counter = 0;

    void Render() override
    {
        glClearColor(19 / 255.0f, 23 / 255.0f, 30 / 255.0f, 1);
        glClear(GL_COLOR_BUFFER_BIT);

        glUseProgram(_shaderProgram);

        if (counter++ % 100 < 50)
        {
            glBindVertexArray(_firstTriangle);
            glDrawArrays(GL_TRIANGLES, 0, 6);
        }
        else
        {
            glBindVertexArray(_secondTriangle);
            glDrawArrays(GL_TRIANGLES, 0, 6);
        }

        glBindVertexArray(0);

        glfwSwapBuffers(_target);
    }

private:
    unsigned int _shaderProgram;

    unsigned int _firstTriangle;
    unsigned int _secondTriangle;

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
