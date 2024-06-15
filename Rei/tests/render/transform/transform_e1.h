#pragma once
#include "../BaseRenderScenario.h"
#include "Engine/Services.h"
#include "glad/glad.h"
#include "glfw/glfw3.h"
#include "glm/fwd.hpp"
#include "glm/ext/matrix_clip_space.hpp"
#include "glm/ext/matrix_transform.hpp"
#include "glm/gtx/vector_angle.inl"
#include "Modules/Render/Shaders/Shader.h"
#include "Modules/Render/Textures/Texture2D.h"

class VertexData
{
public:
    u32 VAO, VBO;
    glm::mat4 model = glm::mat4(1.0f);

public:
    VertexData()
    {
        glGenVertexArrays(1, &VAO);
        glGenBuffers(1, &VBO);

        float vertices[] = {
            -0.5f, -0.5f, -0.5f, 0.0f, 0.0f,
            0.5f, -0.5f, -0.5f, 1.0f, 0.0f,
            0.5f, 0.5f, -0.5f, 1.0f, 1.0f,
            0.5f, 0.5f, -0.5f, 1.0f, 1.0f,
            -0.5f, 0.5f, -0.5f, 0.0f, 1.0f,
            -0.5f, -0.5f, -0.5f, 0.0f, 0.0f,

            -0.5f, -0.5f, 0.5f, 0.0f, 0.0f,
            0.5f, -0.5f, 0.5f, 1.0f, 0.0f,
            0.5f, 0.5f, 0.5f, 1.0f, 1.0f,
            0.5f, 0.5f, 0.5f, 1.0f, 1.0f,
            -0.5f, 0.5f, 0.5f, 0.0f, 1.0f,
            -0.5f, -0.5f, 0.5f, 0.0f, 0.0f,

            -0.5f, 0.5f, 0.5f, 1.0f, 0.0f,
            -0.5f, 0.5f, -0.5f, 1.0f, 1.0f,
            -0.5f, -0.5f, -0.5f, 0.0f, 1.0f,
            -0.5f, -0.5f, -0.5f, 0.0f, 1.0f,
            -0.5f, -0.5f, 0.5f, 0.0f, 0.0f,
            -0.5f, 0.5f, 0.5f, 1.0f, 0.0f,

            0.5f, 0.5f, 0.5f, 1.0f, 0.0f,
            0.5f, 0.5f, -0.5f, 1.0f, 1.0f,
            0.5f, -0.5f, -0.5f, 0.0f, 1.0f,
            0.5f, -0.5f, -0.5f, 0.0f, 1.0f,
            0.5f, -0.5f, 0.5f, 0.0f, 0.0f,
            0.5f, 0.5f, 0.5f, 1.0f, 0.0f,

            -0.5f, -0.5f, -0.5f, 0.0f, 1.0f,
            0.5f, -0.5f, -0.5f, 1.0f, 1.0f,
            0.5f, -0.5f, 0.5f, 1.0f, 0.0f,
            0.5f, -0.5f, 0.5f, 1.0f, 0.0f,
            -0.5f, -0.5f, 0.5f, 0.0f, 0.0f,
            -0.5f, -0.5f, -0.5f, 0.0f, 1.0f,

            -0.5f, 0.5f, -0.5f, 0.0f, 1.0f,
            0.5f, 0.5f, -0.5f, 1.0f, 1.0f,
            0.5f, 0.5f, 0.5f, 1.0f, 0.0f,
            0.5f, 0.5f, 0.5f, 1.0f, 0.0f,
            -0.5f, 0.5f, 0.5f, 0.0f, 0.0f,
            -0.5f, 0.5f, -0.5f, 0.0f, 1.0f
        };

        glBindVertexArray(VAO);

        glBindBuffer(GL_ARRAY_BUFFER, VBO);
        glBufferData(GL_ARRAY_BUFFER, sizeof(vertices), vertices, GL_STATIC_DRAW);

        // position attribute
        glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, 5 * sizeof(float), (void*)0);
        glEnableVertexAttribArray(0);

        // texture coord attribute
        glVertexAttribPointer(1, 2, GL_FLOAT, GL_FALSE, 5 * sizeof(float), (void*)(3 * sizeof(float)));
        glEnableVertexAttribArray(1);

        glBindBuffer(GL_ARRAY_BUFFER, 0);
        glBindVertexArray(0);
    }

    void SetTransform(glm::vec3 translation, f32 rotation)
    {
        model = translate(model, translation);
        model = rotate(model, rotation, glm::vec3(0.0, 0.0, 1.0));
        model = scale(model, glm::vec3(1));
    }

    ~VertexData()
    {
        glDeleteVertexArrays(1, &VAO);
        glDeleteBuffers(1, &VBO);
    }
};

class transform_e1 : public BaseRenderScenario
{
public:
    explicit transform_e1(GLFWwindow* target)
        : BaseRenderScenario(target),
          _shader(rei::GetAssetManager().LoadById<rei::render::Shader>("661984c0-4c8d-46a5-8451-e92d8d6c5cd5")),
          _firstTexture(rei::GetAssetManager().LoadById<rei::render::Texture2D>("702e3f65-78ed-46e7-a611-b640a387b10d")),
          _secondTexture(rei::GetAssetManager().LoadById<rei::render::Texture2D>("0d3c40b8-e7bd-4c79-8662-0489c8203c23"))
    {
    }

    void Dispose() override
    {
    }

    void Setup() override
    {
        glEnable(GL_DEPTH_TEST);
        glEnable(GL_BLEND);
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);

        _shader.Use();
        _shader.SetInt("texture1", 0);
        _shader.SetInt("texture2", 1);

        auto view = glm::mat4(1.0f);
        view = translate(view, glm::vec3(0.0f, 0.0f, -3.0f));

        glm::mat4 projection = glm::perspective(glm::radians(90.0f), 600.0f / 600.0f, 0.1f, 100.0f);

        _shader.SetMatrix4f("view", view);
        _shader.SetMatrix4f("projection", projection);
    }

    f32 _timeScale = 1.f;

    void Render() override
    {
        auto time = static_cast<float>(glfwGetTime()) * _timeScale;
        auto sinTime = static_cast<float>(sin(time));

        glClearColor(19 / 255.0f, 23 / 255.0f, 30 / 255.0f, 1);
        glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);

        _shader.Use();

        glActiveTexture(GL_TEXTURE0);
        _firstTexture.Use();
        glActiveTexture(GL_TEXTURE1);
        _secondTexture.Use();

        glm::vec3 cubePositions[] = {
            glm::vec3(0.0f, 0.0f, 0.0f),
            glm::vec3(2.0f, 5.0f, -15.0f),
            glm::vec3(-1.5f, -2.2f, -2.5f),
            glm::vec3(-3.8f, -2.0f, -12.3f),
            glm::vec3(2.4f, -0.4f, -3.5f),
            glm::vec3(-1.7f, 3.0f, -7.5f),
            glm::vec3(1.3f, -2.0f, -2.5f),
            glm::vec3(1.5f, 2.0f, -2.5f),
            glm::vec3(1.5f, 0.2f, -1.5f),
            glm::vec3(-1.3f, 1.0f, -1.5f)
        };

        glBindVertexArray(_object0.VAO);

        for (i32 i = 0; i < 10; i++)
        {
            glm::mat4 model = glm::mat4(1.0f);
            model = glm::translate(model, cubePositions[i]);
            float angle = 20.0f * i + (time * 100); 
            model = glm::rotate(model, glm::radians(angle), glm::vec3(1.0f, 0.3f, 0.5f));
            _shader.SetMatrix4f("model", model);
            
            glDrawArrays(GL_TRIANGLES, 0, 36);
        }

        glBindVertexArray(0);

        glfwSwapBuffers(_target);
    }

private:
    rei::render::Shader _shader;
    rei::render::Texture2D _firstTexture;
    rei::render::Texture2D _secondTexture;

    VertexData _object0;
};
