#pragma once
#include "../BaseRenderScenario.h"
#include "Engine/Services.h"
#include "glad/glad.h"
#include "glfw/glfw3.h"
#include "glm/fwd.hpp"
#include "glm/ext/matrix_transform.hpp"
#include "glm/gtx/vector_angle.inl"
#include "Modules/Render/Shaders/Shader.h"
#include "Modules/Render/Textures/Texture.h"

class VertexData
{
public:
    u32 VAO, VBO, VEB;

public:
    VertexData()
    {
        glGenVertexArrays(1, &VAO);
        glGenBuffers(1, &VBO);
        glGenBuffers(1, &VEB);

        float vertices[] = {
            // positions          // texture coords           
            0.5f, 0.5f, 0.0f, 2.0f, 2.0f, // top right
            0.5f, -0.5f, 0.0f, 2.0f, 0.0f, // bottom right
            -0.5f, -0.5f, 0.0f, 0.0f, 0.0f, // bottom left
            -0.5f, 0.5f, 0.0f, 0.0f, 2.0f // top left 
        };

        unsigned int indices[] = {
            0, 1, 3, // first triangle
            1, 2, 3 // second triangle
        };

        glBindVertexArray(VAO);

        glBindBuffer(GL_ARRAY_BUFFER, VBO);
        glBufferData(GL_ARRAY_BUFFER, sizeof(vertices), vertices, GL_STATIC_DRAW);

        glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, VEB);
        glBufferData(GL_ELEMENT_ARRAY_BUFFER, sizeof(indices), indices,GL_STATIC_DRAW);

        // position attribute
        glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, 5 * sizeof(float), (void*)0);
        glEnableVertexAttribArray(0);

        // texture coord attribute
        glVertexAttribPointer(1, 2, GL_FLOAT, GL_FALSE, 5 * sizeof(float), (void*)(3 * sizeof(float)));
        glEnableVertexAttribArray(1);

        glBindBuffer(GL_ARRAY_BUFFER, 0);
        glBindVertexArray(0);
    }

    ~VertexData()
    {
        glDeleteVertexArrays(1, &VAO);
        glDeleteBuffers(1, &VBO);
        glDeleteBuffers(1, &VEB);
    }
};

class transform_e0 : public BaseRenderScenario
{
public:
    explicit transform_e0(GLFWwindow* target)
        : BaseRenderScenario(target),
          _shader(rei::GetAssetManager().GetByPath<rei::render::Shader>("C:/Repos/Rei/Rei/resources/shaders/test/test_3.rshader")),
          _firstTexture(rei::GetAssetManager().GetByPath<rei::render::Texture>("C:/Repos/Rei/Rei/resources/textures/test_texture.png")),
          _secondTexture(rei::GetAssetManager().GetByPath<rei::render::Texture>("C:/Repos/Rei/Rei/resources/textures/ring.png"))
    {
    }

    void Dispose() override
    {
    }

    void Setup() override
    {
        glEnable(GL_BLEND);
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);

        _shader->Use();
        _shader->SetInt("texture1", 0);
        _shader->SetInt("texture2", 1);
    }

    void SetTransform(glm::vec3 translation, f32 rotation)
    {
        auto trans = glm::mat4(1.0f);
        trans = translate(trans, translation);
        trans = rotate(trans, rotation, glm::vec3(0.0, 0.0, 1.0));
        trans = scale(trans, glm::vec3(1));

        _shader->SetMatrix4f("transform", trans);
    }

    f32 speedMultiplier = 1;

    void Render() override
    {
        auto time = static_cast<float>(glfwGetTime()) * speedMultiplier;
        auto sinTime = static_cast<float>(sin(time));
        speedMultiplier += 0.001f;

        glClearColor(19 / 255.0f, 23 / 255.0f, 30 / 255.0f, 1);
        glClear(GL_COLOR_BUFFER_BIT);

        _shader->Use();

        glActiveTexture(GL_TEXTURE0);
        _firstTexture->Use();
        glActiveTexture(GL_TEXTURE1);
        _secondTexture->Use();

        SetTransform(glm::vec3(sinTime), time);
        glBindVertexArray(_object0.VAO);
        glDrawElements(GL_TRIANGLES, 6, GL_UNSIGNED_INT, 0);
        glBindVertexArray(0);

        SetTransform(glm::vec3(-sinTime), time);
        glBindVertexArray(_object1.VAO);
        glDrawElements(GL_TRIANGLES, 6, GL_UNSIGNED_INT, 0);
        glBindVertexArray(0);

        SetTransform(glm::vec3(sinTime, -sinTime, 0), time);
        glBindVertexArray(_object2.VAO);
        glDrawElements(GL_TRIANGLES, 6, GL_UNSIGNED_INT, 0);
        glBindVertexArray(0);

        SetTransform(glm::vec3(-sinTime, sinTime, 0), time);
        glBindVertexArray(_object3.VAO);
        glDrawElements(GL_TRIANGLES, 6, GL_UNSIGNED_INT, 0);
        glBindVertexArray(0);

        glfwSwapBuffers(_target);
    }

private:
    rei::assets::AssetRef<rei::render::Shader> _shader;
    rei::assets::AssetRef<rei::render::Texture> _firstTexture;
    rei::assets::AssetRef<rei::render::Texture> _secondTexture;

    VertexData _object0;
    VertexData _object1;
    VertexData _object2;
    VertexData _object3;
};
