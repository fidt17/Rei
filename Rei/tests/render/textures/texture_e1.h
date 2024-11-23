#pragma once
#include "../BaseRenderScenario.h"
#include "Engine/Services.h"
#include "glad/glad.h"
#include "glfw/glfw3.h"
#include "Modules/Render/Shaders/Shader.h"
#include "Modules/Render/Textures/Texture.h"

class texture_e1 : public BaseRenderScenario
{
public:
    explicit texture_e1(GLFWwindow* target)
        : BaseRenderScenario(target),
          _shader(rei::GetAssetManager().LoadById<rei::render::Shader>("58e480d1-7143-40ab-b2c6-1dd24c3a7142")), // test_2.rshader
          _firstTexture(rei::GetAssetManager().LoadById<rei::render::Texture>("6750146c-8a5e-4fcd-80d1-18fbb37e950d")), // test_texture.png
          _secondTexture(rei::GetAssetManager().LoadById<rei::render::Texture>("8ba7a9d6-df0a-4951-9743-62732f786d01")) // ring.png
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


        glActiveTexture(GL_TEXTURE0);
        _firstTexture.Use();
        glActiveTexture(GL_TEXTURE1);
        _secondTexture.Use();
        
        _shader.Use();
        
        glBindVertexArray(_vertexArray);

        glDrawElements(GL_TRIANGLES, 6, GL_UNSIGNED_INT, 0);

        glBindVertexArray(0);

        glfwSwapBuffers(_target);
    }

private:
    rei::render::Shader _shader;
    rei::render::Texture _firstTexture;
    rei::render::Texture _secondTexture;
    unsigned int _vertexBuffer, _vertexArray, _elementBuffer;
};
