#pragma once
#include "Engine/Engine.h"
#include "Engine/Services.h"
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
    rei::render::Shader _shader = rei::GetAssetManager().LoadById<rei::render::Shader>("18f9681e-8003-485d-b584-f1a2812e3348");
    unsigned int _vertexBuffer, _vertexArray;
};
