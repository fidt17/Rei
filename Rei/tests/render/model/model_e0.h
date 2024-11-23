#pragma once
#include "../BaseRenderScenario.h"
#include "glfw/glfw3.h"
#include "glm/ext/matrix_transform.hpp"
#include "Modules/Render/Model/Model.h"
#include "../../../resources/rei_behaviours/render/light/AmbientLight.h"
#include "../../../resources/rei_behaviours/render/light/PointLight.h"
#include "../../../resources/rei_behaviours/transformation/Transform.h"

#define POINT_LIGHTS_COUNT 4

class BoxVertexData
{
public:
    u32 VAO, VBO;
    glm::mat4 model = glm::mat4(1.0f);

public:
    BoxVertexData()
    {
        glGenVertexArrays(1, &VAO);
        glGenBuffers(1, &VBO);

        float vertices[] = {
            -0.5f, -0.5f, -0.5f, 0.0f, 0.0f, -1.0f,
            0.5f, -0.5f, -0.5f, 0.0f, 0.0f, -1.0f,
            0.5f, 0.5f, -0.5f, 0.0f, 0.0f, -1.0f,
            0.5f, 0.5f, -0.5f, 0.0f, 0.0f, -1.0f,
            -0.5f, 0.5f, -0.5f, 0.0f, 0.0f, -1.0f,
            -0.5f, -0.5f, -0.5f, 0.0f, 0.0f, -1.0f,

            -0.5f, -0.5f, 0.5f, 0.0f, 0.0f, 1.0f,
            0.5f, -0.5f, 0.5f, 0.0f, 0.0f, 1.0f,
            0.5f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f,
            0.5f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f,
            -0.5f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f,
            -0.5f, -0.5f, 0.5f, 0.0f, 0.0f, 1.0f,

            -0.5f, 0.5f, 0.5f, -1.0f, 0.0f, 0.0f,
            -0.5f, 0.5f, -0.5f, -1.0f, 0.0f, 0.0f,
            -0.5f, -0.5f, -0.5f, -1.0f, 0.0f, 0.0f,
            -0.5f, -0.5f, -0.5f, -1.0f, 0.0f, 0.0f,
            -0.5f, -0.5f, 0.5f, -1.0f, 0.0f, 0.0f,
            -0.5f, 0.5f, 0.5f, -1.0f, 0.0f, 0.0f,

            0.5f, 0.5f, 0.5f, 1.0f, 0.0f, 0.0f,
            0.5f, 0.5f, -0.5f, 1.0f, 0.0f, 0.0f,
            0.5f, -0.5f, -0.5f, 1.0f, 0.0f, 0.0f,
            0.5f, -0.5f, -0.5f, 1.0f, 0.0f, 0.0f,
            0.5f, -0.5f, 0.5f, 1.0f, 0.0f, 0.0f,
            0.5f, 0.5f, 0.5f, 1.0f, 0.0f, 0.0f,

            -0.5f, -0.5f, -0.5f, 0.0f, -1.0f, 0.0f,
            0.5f, -0.5f, -0.5f, 0.0f, -1.0f, 0.0f,
            0.5f, -0.5f, 0.5f, 0.0f, -1.0f, 0.0f,
            0.5f, -0.5f, 0.5f, 0.0f, -1.0f, 0.0f,
            -0.5f, -0.5f, 0.5f, 0.0f, -1.0f, 0.0f,
            -0.5f, -0.5f, -0.5f, 0.0f, -1.0f, 0.0f,

            -0.5f, 0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
            0.5f, 0.5f, -0.5f, 0.0f, 1.0f, 0.0f,
            0.5f, 0.5f, 0.5f, 0.0f, 1.0f, 0.0f,
            0.5f, 0.5f, 0.5f, 0.0f, 1.0f, 0.0f,
            -0.5f, 0.5f, 0.5f, 0.0f, 1.0f, 0.0f,
            -0.5f, 0.5f, -0.5f, 0.0f, 1.0f, 0.0f
        };

        glBindVertexArray(VAO);

        glBindBuffer(GL_ARRAY_BUFFER, VBO);
        glBufferData(GL_ARRAY_BUFFER, sizeof(vertices), vertices, GL_STATIC_DRAW);

        // position attribute
        glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, 6 * sizeof(float), (void*)0);
        glEnableVertexAttribArray(0);
        // normal attribute
        glVertexAttribPointer(1, 3, GL_FLOAT, GL_FALSE, 6 * sizeof(float), (void*)(3 * sizeof(float)));
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

    ~BoxVertexData()
    {
        glDeleteVertexArrays(1, &VAO);
        glDeleteBuffers(1, &VBO);
    }
};

class model_e0 : public BaseRenderScenario
{
public:
    explicit model_e0(GLFWwindow* target)
        : BaseRenderScenario(target),
          _shader(rei::GetAssetManager().LoadById<rei::render::Shader>("30429fc8-e274-4503-811a-441099a29e66")), // lit.rshader
          _lightSourceShader(rei::GetAssetManager().LoadById<rei::render::Shader>("d887c985-f2da-4c89-b62c-b329654839cb")), // light_source.rshader
          _model("C:/Repos/Rei/TMP/backpack/backpack.obj")
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
        glEnable(GL_MULTISAMPLE);
        //glPolygonMode(GL_FRONT_AND_BACK, GL_LINE);
    }

    void Render() override
    {
        glClearColor(19 / 255.0f, 23 / 255.0f, 30 / 255.0f, 1);
        glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);

        RenderModel(glm::vec3(0,sin(glfwGetTime()),0));

        for (auto& light : _lights)
        {
            RenderPointLight(light);
        }

        glfwSwapBuffers(_target);
    }

    void RenderModel(glm::vec3 offset)
    {
        SetAmbientLight(_shader);
        SetPointLights(_shader);

        _shader.SetFloat("_Shininess", 1);

        _shader.SetMatrix4f("projection", _camera.Get().GetProjectionMatrix());
        _shader.SetMatrix4f("view", _camera.Get().GetViewMatrix());

        auto model = glm::mat4(1.0f);
        model = translate(model, offset);
        model = rotate(model, 180.0f, glm::vec3(0,1,0));
        model = scale(model, glm::vec3(1.0f, 1.0f, 1.0f));
        _shader.SetMatrix4f("model", model);

        _model.Draw(_shader);
    }

    void RenderPointLight(const rei::ecs::RefComponent<rei::behaviour::PointLight>& light) const
    {
        if (light.IsNull()) return;

        _lightSourceShader.SetColor("_Color", light.Get().GetColor());
        _lightSourceShader.SetFloat("_Strength", light.Get().GetStrength());

        _lightSourceShader.SetMatrix4f("projection", _camera.Get().GetProjectionMatrix());
        _lightSourceShader.SetMatrix4f("view", _camera.Get().GetViewMatrix());

        glBindVertexArray(_lightBox.VAO);

        glm::mat4 model = glm::mat4(1.0f);
        model = translate(model, glm::vec3(light.Get().GetTransform().GetPosition()));
        model = scale(model, glm::vec3(0.02f));
        _lightSourceShader.SetMatrix4f("model", model);

        glDrawArrays(GL_TRIANGLES, 0, 36);

        glBindVertexArray(0);
    }

    void SetAmbientLight(const rei::render::Shader& shader) const
    {
        ECS_WORLD(rei::GetInternalWorld());
        auto f = rei::GetInternalWorld().GetFiltersRegistry()->Get<rei::render::AmbientLight>();
        rei::GetInternalWorld().RefreshAll();

        if (f->GetEntitiesCount() == 0) return;
        const rei::render::AmbientLight& ambientLight = GET_REF(*f->begin(), rei::render::AmbientLight);

        shader.SetFloat("_AmbientLight.Strength", ambientLight.GetStrength());

        auto c = ambientLight.GetColor();
        shader.SetColor("_AmbientLight.Color", c);
    }

    void SetPointLights(const rei::render::Shader& shader)
    {
        ECS_WORLD(rei::GetInternalWorld());
        const auto f = rei::GetInternalWorld().GetFiltersRegistry()->Get<rei::behaviour::PointLight>();
        rei::GetInternalWorld().RefreshAll();

        _lights.clear();
        i32 lightsCount = 0;
        FOR(e, f)
        {
            _lights.emplace_back(GET_REF(e, rei::behaviour::PointLight));
            lightsCount++;
            if (lightsCount >= POINT_LIGHTS_COUNT) break;
        }

        i32 idx = 0;
        for (auto& light : _lights)
        {
            if (light.IsNull()) continue;

            shader.SetVector3("_PointLights[" + std::to_string(idx) + "].Position", light.Get().GetTransform().GetPosition());
            shader.SetFloat("_PointLights[" + std::to_string(idx) + "].Strength", light.Get().GetStrength());
            shader.SetColor("_PointLights[" + std::to_string(idx) + "].Color", light.Get().GetColor());

            idx += 1;
        }
    }

private:
    rei::render::Shader _shader;
    rei::render::Shader _lightSourceShader;
    rei::render::Model _model;

    BoxVertexData _lightBox;
    std::vector<rei::ecs::RefComponent<rei::behaviour::PointLight>> _lights;
};
