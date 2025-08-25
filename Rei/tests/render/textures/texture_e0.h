#pragma once
#include "../BaseRenderScenario.h
#include "../../../resources/rei_behaviours/transformation/Transform.h"
#include "Engine/Services.h"
#include "glad/glad.h"
#include "GLFW/glfw3.h"
#include "Modules/Render/RenderScenario/BaseRenderScenario.h"
#include "Modules/Render/Shaders/Shader.h"
#include "Modules/Render/Shaders/ShaderUtility.h"
#include "Modules/Render/Textures/Texture.h"

class texture_e0 : public rei::render::BaseRenderScenario
{
public:
    explicit texture_e0(GLFWwindow* target)
        : BaseRenderScenario(target),
          _shader(rei::GetAssetManager().GetByPath<rei::render::Shader>("C:/Repos/Rei/Rei/resources/shaders/unlit.rshader")),
          _texture(rei::GetAssetManager().GetByPath<rei::render::Texture>("C:/Repos/Rei/Rei/resources/textures/ring.png"))
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
            0.5f, 0.5f, 0.0f, 1.0f, 0.0f, 0.0f, 1.0f, 1.0f, // top right
            0.5f, -0.5f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f, // bottom right
            -0.5f, -0.5f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, // bottom left
            -0.5f, 0.5f, 0.0f, 1.0f, 1.0f, 0.0f, 0.0f, 1.0f // top left 
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
    }

    void Render() override
    {
        glClearColor(19 / 255.0f, 23 / 255.0f, 30 / 255.0f, 1);
        glClear(GL_COLOR_BUFFER_BIT);

        _shader->Use();

        _shader->Use();
        _shader->SetMatrix4f("projection", _camera.Get().GetProjectionMatrix());
        _shader->SetMatrix4f("view", _camera.Get().GetViewMatrix());
        auto model = glm::mat4(1.0f);
        model = translate(model, glm::vec3(0.0f, 0.0f, 0.0f)); // translate it down so it's at the center of the scene
        model = scale(model, glm::vec3(1.0f, 1.0f, 1.0f)); // it's a bit too big for our scene, so scale it down
        _shader->SetMatrix4f("model", model);

        _texture->Use();

        glBindVertexArray(_vertexArray);

        glDrawElements(GL_TRIANGLES, 6, GL_UNSIGNED_INT, 0);

        glBindVertexArray(0);

        glfwSwapBuffers(_target);
    }

private:
    rei::assets::AssetRef<rei::render::Shader> _shader;
    rei::assets::AssetRef<rei::render::Texture> _texture;

    unsigned int _vertexBuffer, _vertexArray, _elementBuffer;
};
