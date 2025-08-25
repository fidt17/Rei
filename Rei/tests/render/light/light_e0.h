#pragma once
#include "../../../resources/rei_behaviours/render/light/AmbientLight.h"
#include "../../../resources/rei_behaviours/render/light/PointLight.h"
#include "../../../resources/rei_behaviours/transformation/Transform.h"
#include "Engine/Services.h"
#include "Modules/Render/RenderScenario/BaseRenderScenario.h"
#include "Modules/Render/Shaders/Shader.h"

namespace rei::render
{
    class AmbientLight;
}

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

constexpr int POINT_LIGHTS_COUNT = 4;

class light_e0 : public rei::render::BaseRenderScenario
{
public:
    explicit light_e0(GLFWwindow* target)
        : BaseRenderScenario(target),
          _boxShader(rei::GetAssetManager().GetByPath<rei::render::Shader>("C:/Repos/Rei/Rei/resources/shaders/simple_lit.rshader")),
          _lightSourceShader(rei::GetAssetManager().GetByPath<rei::render::Shader>("C:/Repos/Rei/Rei/resources/shaders/light_source.rshader"))
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

        for (auto& light : _lights)
        {
            RenderPointLight(light);
        }

        RenderBox();

        glfwSwapBuffers(_target);
    }

    void ConfigureAmbientLight(const rei::render::Shader& shader) const
    {
        ECS_WORLD(rei::GetInternalWorld());
        auto f = rei::GetInternalWorld().GetFiltersRegistry()->Get<rei::render::AmbientLight>();
        rei::GetInternalWorld().RefreshAll();

        if (f->GetEntitiesCount() == 0) return;
        const rei::render::AmbientLight& ambientLight = GET_REF(*f->begin(), rei::render::AmbientLight);

        shader.SetFloat("_AmbientLight.Strength", ambientLight.GetStrength());

        auto c = ambientLight.GetColor();
        shader.SetColor("_AmbientLight.Color", ambientLight.GetColor());
    }

    void ConfigurePointLights(const rei::render::Shader& shader)
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

    void RenderPointLight(const rei::ecs::RefComponent<rei::behaviour::PointLight>& light)
    {
        if (light.IsNull()) return;

        _lightSourceShader->SetColor("_Color", light.Get().GetColor());
        _lightSourceShader->SetFloat("_Strength", light.Get().GetStrength());
        _lightSourceShader->SetMatrix4f("projection", _camera.Get().GetProjectionMatrix());
        _lightSourceShader->SetMatrix4f("view", _camera.Get().GetViewMatrix());

        glBindVertexArray(_box.VAO);

        glm::mat4 model = glm::mat4(1.0f);
        model = translate(model, glm::vec3(light.Get().GetTransform().GetPosition()));
        model = scale(model, glm::vec3(0.02f * light.Get().GetStrength()));
        _lightSourceShader->SetMatrix4f("model", model);

        glDrawArrays(GL_TRIANGLES, 0, 36);

        glBindVertexArray(0);
    }

    void RenderBox()
    {
        ConfigureAmbientLight(*_boxShader.Asset);
        ConfigurePointLights(*_boxShader.Asset);

        _boxShader->SetFloat("_Shininess", 1000.f);
        _boxShader->SetColor("_Color", rei::render::Color(0.3f, 0.34f, 0.39f, 1.f));

        _boxShader->SetMatrix4f("projection", _camera.Get().GetProjectionMatrix());
        _boxShader->SetMatrix4f("view", _camera.Get().GetViewMatrix());

        glm::mat4 model = glm::mat4(1.0f);
        model = translate(model, glm::vec3(0,-1,0));
        model = scale(model, glm::vec3(1000, 0.1f, 1000));
        _boxShader->SetMatrix4f("model", model);

        glBindVertexArray(_box.VAO);
        
        glDrawArrays(GL_TRIANGLES, 0, 36);

        glBindVertexArray(0);
    }

    void Dispose() override
    {
    }

private:
    rei::assets::AssetRef<rei::render::Shader> _boxShader;
    rei::assets::AssetRef<rei::render::Shader> _lightSourceShader;

    BoxVertexData _box;
    BoxVertexData _lightSource;
    std::vector<rei::ecs::RefComponent<rei::behaviour::PointLight>> _lights;
};
