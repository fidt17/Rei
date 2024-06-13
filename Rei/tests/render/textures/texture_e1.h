#pragma once
#include "../BaseRenderScenario.h"
#include "Engine/Services.h"
#include "glad/glad.h"
#include "glfw/glfw3.h"
#include "Modules/Render/Shaders/Shader.h"
#include "Modules/Render/Textures/Texture2D.h"

class texture_e1 : public BaseRenderScenario
{
public:
    explicit texture_e1(GLFWwindow* target)
        : BaseRenderScenario(target),
          _shader(rei::GetAssetManager().LoadById<rei::render::Shader>("ec1fef2d-ab64-4ee7-a71e-f75b249d4bd4")),
          _firstTexture(rei::GetAssetManager().LoadById<rei::render::Texture2D>("702e3f65-78ed-46e7-a611-b640a387b10d")),
          _secondTexture(rei::GetAssetManager().LoadById<rei::render::Texture2D>("0d3c40b8-e7bd-4c79-8662-0489c8203c23"))
    {
    }

    void Dispose() override
    {
        glDeleteVertexArrays(1, &_vertexArray);
        glDeleteBuffers(1, &_vertexBuffer);
        glDeleteBuffers(1, &_elementBuffer);
    }

    void ConfigureVertexData()
    {
        glGenVertexArrays(1, &_vertexArray);
        glGenBuffers(1, &_vertexBuffer);
        glGenBuffers(1, &_elementBuffer);

        glBindVertexArray(_vertexArray);

        glBindBuffer(GL_ARRAY_BUFFER, _vertexBuffer);
        /*
        float vertices[] = {
            // positions          // colors           // texture coords
            0.5f, 0.5f, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f, 1.0f, // top right
            0.5f, -0.5f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f, // bottom right
            -0.5f, -0.5f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, // bottom left
            -0.5f, 0.5f, 0.0f, 1.0f, 1.0f, 0.0f, 0.0f, 1.0f // top left 
        };
        */
        float vertices[] = {
            // positions          // colors           // texture coords
            0.5f, 0.5f, 0.0f, 1.0f, 0.0f, 0.0f,       2.0f, 2.0f, // top right
            0.5f, -0.5f, 0.0f, 0.0f, 1.0f, 0.0f,      2.0f, 0.0f, // bottom right
            -0.5f, -0.5f, 0.0f, 0.0f, 0.0f, 1.0f,     0.0f, 0.0f, // bottom left
            -0.5f, 0.5f, 0.0f, 1.0f, 1.0f, 0.0f,      0.0f, 2.0f // top left 
        };
        glBufferData(GL_ARRAY_BUFFER, sizeof(vertices), vertices, GL_STATIC_DRAW);

        glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, _elementBuffer);
        unsigned int indices[] = {
            0, 1, 3, // first triangle
            1, 2, 3 // second triangle
        };
        glBufferData(GL_ELEMENT_ARRAY_BUFFER, sizeof(indices), indices,GL_STATIC_DRAW);

        // position attribute
        glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, 8 * sizeof(float), (void*)0);
        glEnableVertexAttribArray(0);

        // color attribute
        glVertexAttribPointer(1, 3, GL_FLOAT, GL_FALSE, 8 * sizeof(float), (void*)(3 * sizeof(float)));
        glEnableVertexAttribArray(1);

        // texture coord attribute
        glVertexAttribPointer(2, 2, GL_FLOAT, GL_FALSE, 8 * sizeof(float), (void*)(6 * sizeof(float)));
        glEnableVertexAttribArray(2);

        glBindBuffer(GL_ARRAY_BUFFER, 0);
        glBindVertexArray(0);
    }

    void Setup() override
    {
        glEnable(GL_BLEND);
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
        
        ConfigureVertexData();

        _shader.Use();
        _shader.SetInt("texture1", 0);
        _shader.SetInt("texture2", 1);
    }

    void Render() override
    {
        glClearColor(19 / 255.0f, 23 / 255.0f, 30 / 255.0f, 1);
        glClear(GL_COLOR_BUFFER_BIT);

        _shader.Use();

        glActiveTexture(GL_TEXTURE0);
        _firstTexture.Use();
        glActiveTexture(GL_TEXTURE1);
        _secondTexture.Use();
        
        glBindVertexArray(_vertexArray);

        glDrawElements(GL_TRIANGLES, 6, GL_UNSIGNED_INT, 0);

        glBindVertexArray(0);

        glfwSwapBuffers(_target);
    }

private:
    rei::render::Shader _shader;
    rei::render::Texture2D _firstTexture;
    rei::render::Texture2D _secondTexture;
    unsigned int _vertexBuffer, _vertexArray, _elementBuffer;
};
