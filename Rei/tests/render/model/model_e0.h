#pragma once
#include "../../../resources/rei_behaviours/render/MeshRenderer.h"
#include "glfw/glfw3.h"
#include "Modules/Render/Model/Model.h"
#include "../../../resources/rei_behaviours/render/light/AmbientLight.h"
#include "../../../resources/rei_behaviours/render/light/PointLight.h"
#include "../../../resources/rei_behaviours/transformation/Transform.h"
#include "Engine/Engine.h"
#include "Modules/Render/RenderScenario/BaseRenderScenario.h"


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

class model_e0 : public rei::render::BaseRenderScenario
{
public:
    explicit model_e0(GLFWwindow* target)
        : BaseRenderScenario(target),
          _lightSourceShader(rei::GetAssetManager().GetByPath<rei::render::Shader>("C:/Repos/Rei/Rei/resources/shaders/light_source.rshader"))
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

        CreateModel(rei::math::Vector3(0, 0, 0));
    }

    void CreateModel(rei::math::Vector3 position)
    {
        auto model = rei::GetAssetManager().GetByPath<rei::render::Model>("C:/Repos/Rei/TMP/backpack/backpack.obj");
        auto shader = rei::GetAssetManager().GetByPath<rei::render::Shader>("C:/Repos/Rei/Rei/resources/shaders/lit.rshader");
        auto diffuseTexture = rei::GetAssetManager().GetByPath<rei::render::Texture>("C:/Repos/Rei/TMP/backpack/diffuse.jpg");
        diffuseTexture->SetType(rei::render::Diffuse);
        auto specularTexture = rei::GetAssetManager().GetByPath<rei::render::Texture>("C:/Repos/Rei/TMP/backpack/specular.jpg");
        specularTexture->SetType(rei::render::Specular);

        auto material = rei::GetAssetManager().CreateAsset<rei::render::Material>(shader);

        material->GetShader().SetFloat("_Shininess", 3);
        material->GetShader().SetColor("_Color", rei::render::Color(1, 1, 1, 1));

        material->GetTextures().push_back(diffuseTexture);
        material->GetTextures().push_back(specularTexture);

        ECS_WORLD(rei::GetInternalWorld());
        const auto e = NEW_ENTITY();

        auto& transform = ADD_BEHAVIOUR(e, rei::transformation::Transform);
        transform.Reset();

        transform.GetPosition() = position;

        auto& meshRenderer = ADD_BEHAVIOUR(e, rei::render::MeshRenderer);
        meshRenderer.SetModel(model);
        meshRenderer.SetMaterial(material);
    }

    void Render() override
    {
        glClearColor(19 / 255.0f, 23 / 255.0f, 30 / 255.0f, 1);
        glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);

        _projectionMatrix = _camera.Get().GetProjectionMatrix();
        _viewMatrix = _camera.Get().GetViewMatrix();

        FindAmbientLights();
        FindPointLights();
        
        RenderMeshRenderers();

        for (auto& light : _pointLights)
        {
            RenderPointLight(light);
        }

        glfwSwapBuffers(_target);
    }

    void RenderMeshRenderers()
    {
        ECS_WORLD(rei::GetInternalWorld());
        const auto f = rei::GetInternalWorld().GetFiltersRegistry()->Get<rei::render::MeshRenderer>();
        rei::GetInternalWorld().RefreshAll();

        FOR(e, f)
        {
            auto& meshRenderer = GET(e, rei::render::MeshRenderer);

            const rei::render::Shader& shader = meshRenderer.GetRenderShader();
            
            SetAmbientLight(shader);
            SetPointLights(shader);

            shader.SetViewMatrices(_projectionMatrix, _viewMatrix, meshRenderer.GetTransform().CalculateModelMatrix());
            meshRenderer.Render();
        }
    }

    void RenderPointLight(const rei::ecs::RefComponent<rei::render::PointLight>& light)
    {
        if (light.IsNull()) return;

        _lightSourceShader->SetColor("_Color", light.Get().GetColor());
        _lightSourceShader->SetFloat("_Strength", light.Get().GetStrength());

        _lightSourceShader->SetViewMatrices(_projectionMatrix, _viewMatrix, light.Get().GetTransform().CalculateModelMatrix());

        glBindVertexArray(_lightBox.VAO);
        glDrawArrays(GL_TRIANGLES, 0, 36);
        glBindVertexArray(0);
    }

    void SetAmbientLight(const rei::render::Shader& shader) const
    {
        if (_ambientLight.IsNull())
        {
            shader.SetFloat("_AmbientLight.Strength", 0);
            shader.SetColor("_AmbientLight.Color", rei::render::Color(0, 0, 0, 1));
            return;
        }

        shader.SetFloat("_AmbientLight.Strength", _ambientLight.Get().GetStrength());

        auto c = _ambientLight.Get().GetColor();
        shader.SetColor("_AmbientLight.Color", c);
    }

    void SetPointLights(const rei::render::Shader& shader) const
    {
        for (int i = 0; i < _pointLights.size(); i++)
        {
            auto& light = _pointLights[i];
            if (light.IsNull()) continue;

            shader.SetVector3("_PointLights[" + std::to_string(i) + "].Position", light.Get().GetTransform().GetPosition());
            shader.SetFloat("_PointLights[" + std::to_string(i) + "].Strength", light.Get().GetStrength());
            shader.SetColor("_PointLights[" + std::to_string(i) + "].Color", light.Get().GetColor());
        }
    }

    void FindAmbientLights()
    {
        ECS_WORLD(rei::GetInternalWorld());
        const auto f = rei::GetInternalWorld().GetFiltersRegistry()->Get<rei::render::AmbientLight>();
        rei::GetInternalWorld().RefreshAll();

        if (f->GetEntitiesCount() == 0) return;
        _ambientLight = GET_REF(*f->begin(), rei::render::AmbientLight);
    }

    void FindPointLights()
    {
        ECS_WORLD(rei::GetInternalWorld());
        const auto f = rei::GetInternalWorld().GetFiltersRegistry()->Get<rei::render::PointLight>();
        rei::GetInternalWorld().RefreshAll();

        _pointLights.clear();
        i32 lightsCount = 0;
        FOR(e, f)
        {
            _pointLights.emplace_back(GET_REF(e, rei::render::PointLight));
            lightsCount++;
            if (lightsCount >= REI_MAX_POINT_LIGHTS_COUNT) break;
        }
    }

private:
    glm::mat4 _projectionMatrix;
    glm::mat4 _viewMatrix;

    rei::assets::AssetRef<rei::render::Shader> _lightSourceShader;

    BoxVertexData _lightBox;

    rei::ecs::RefComponent<rei::render::AmbientLight> _ambientLight;
    std::vector<rei::ecs::RefComponent<rei::render::PointLight>> _pointLights;
};
