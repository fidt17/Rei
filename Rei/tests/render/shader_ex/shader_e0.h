#pragma once
#include "glad/glad.h"
#include "glfw/glfw3.h"
#include "Modules/Render/Shaders/Shader.h"
#include "Modules/Render/Shaders/ShaderUtility.h"

class BaseRenderScenario;

class shader_e0 final : public BaseRenderScenario
{
public:
    explicit shader_e0(GLFWwindow* target)
        : BaseRenderScenario(target),
          _vertexBuffer(0), _vertexArray(0)
    {
    }

    void ConfigureVertexData()
    {
        glGenVertexArrays(1, &_vertexArray);
        glGenBuffers(1, &_vertexBuffer);

        glBindVertexArray(_vertexArray);

        glBindBuffer(GL_ARRAY_BUFFER, _vertexBuffer);
        float vertices[] = {
            // positions        // colors
            1.0f, -1.0f, 0.0f, 1.0f, 0.0f, 0.0f, // bottom right
            -1.0f, -1.0f, 0.0f, 0.0f, 1.0f, 0.0f, // bottom left
            0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 1.0f // top
        };
        glBufferData(GL_ARRAY_BUFFER, sizeof(vertices), vertices, GL_STATIC_DRAW);

        glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, 6 * sizeof(float), (void*)0);
        glEnableVertexAttribArray(0);
        glVertexAttribPointer(1, 3, GL_FLOAT, GL_FALSE, 6 * sizeof(float), (void*)(3 * sizeof(float)));
        glEnableVertexAttribArray(1);

        glBindBuffer(GL_ARRAY_BUFFER, 0);
        glBindVertexArray(0);
    }

    void Setup() override
    {
        ConfigureVertexData();

        // uncomment this call to draw in wireframe polygons.
        //glPolygonMode(GL_FRONT_AND_BACK, GL_LINE);
    }

    void Render() override
    {
        glClearColor(19 / 255.0f, 23 / 255.0f, 30 / 255.0f, 1);
        glClear(GL_COLOR_BUFFER_BIT);

        _shader.Use();
        _shader.SetFloat("xOffset", sin(glfwGetTime()));

        glBindVertexArray(_vertexArray);
        glDrawArrays(GL_TRIANGLES, 0, 3);

        glBindVertexArray(0);

        glfwSwapBuffers(_target);
    }

private:
    const char* _vertexShaderSource = "#version 330 core\n"
        "layout (location = 0) in vec3 aPos;\n"
        "layout (location = 1) in vec3 aColor;\n"
        "out vec3 screenPos;\n"
        "uniform float xOffset;\n"
        "void main()\n"
        "{\n"
        "   gl_Position = vec4(aPos.x + xOffset, aPos.y, aPos.z, 1.0);\n"
        "   screenPos = aPos;\n"
        "}\0";

    const char* _fragmentShaderSource = "#version 330 core\n"
        "in vec3 screenPos;\n"
        "out vec4 FragColor;\n"
        "void main()\n"
        "{\n"
        "   float dist  = 1 - distance(screenPos, vec3(0f, 0f, 0));\n"
        "   dist = pow(dist, 10);\n"
        "   FragColor = vec4(dist, dist, dist, 1f);\n"
        "}\n\0";
    
    rei::render::Shader _shader = {std::string (_vertexShaderSource).c_str(), std::string (_fragmentShaderSource).c_str()};
    unsigned int _vertexBuffer, _vertexArray;
};
