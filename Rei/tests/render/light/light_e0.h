#pragma once
#include "../BaseRenderScenario.h"
#include "Engine/Services.h"
#include "Modules/Render/Shaders/Shader.h"

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

class light_e0 : public BaseRenderScenario
{
public:
    explicit light_e0(GLFWwindow* target)
        : BaseRenderScenario(target),
          _boxShader(rei::GetAssetManager().LoadById<rei::render::Shader>("8c7da4b5-1635-456c-bf99-21917f332928")),
          _lightSourceShader(rei::GetAssetManager().LoadById<rei::render::Shader>("d887c985-f2da-4c89-b62c-b329654839cb"))
    {
    }

    void Setup() override
    {
        glEnable(GL_DEPTH_TEST);
        glEnable(GL_BLEND);
        glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
        glEnable(GL_MULTISAMPLE);
    }

    void Render() override
    {
        if (_camera.IsNull()) return;

        glClearColor(19 / 255.0f, 23 / 255.0f, 30 / 255.0f, 1);
        glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);

        RenderLightSource();
        RenderBox();

        glfwSwapBuffers(_target);
    }

    void RenderLightSource()
    {
        _lightSourceShader.SetMatrix4f("projection", _camera.Get().GetProjectionMatrix());
        _lightSourceShader.SetMatrix4f("view", _camera.Get().GetViewMatrix());

        glBindVertexArray(_box.VAO);

        glm::mat4 model = glm::mat4(1.0f);
        model = translate(model, glm::vec3(0, 1, 1));
        model = scale(model, glm::vec3(0.2f));
        _lightSourceShader.SetMatrix4f("model", model);

        glDrawArrays(GL_TRIANGLES, 0, 36);

        glBindVertexArray(0);
    }

    void RenderBox()
    {
        _boxShader.SetMatrix4f("projection", _camera.Get().GetProjectionMatrix());
        _boxShader.SetMatrix4f("view", _camera.Get().GetViewMatrix());
        _boxShader.SetVector3("objectColor", rei::math::Vector3(1.0f, 0.5f, 0.31f));
        _boxShader.SetVector3("lightColor", rei::math::Vector3(1.0f, 1.0f, 1.0f));

        glBindVertexArray(_box.VAO);

        const glm::mat4 model = glm::mat4(1.0f);
        _boxShader.SetMatrix4f("model", model);

        glDrawArrays(GL_TRIANGLES, 0, 36);

        glBindVertexArray(0);
    }

    void Dispose() override
    {
    }

private:
    rei::render::Shader _boxShader;
    rei::render::Shader _lightSourceShader;
    
    VertexData _box;
};
