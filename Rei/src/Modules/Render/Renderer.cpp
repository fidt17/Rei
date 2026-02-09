#include "Renderer.h"

#include "Material/Material.h"
#include "RenderScenario/DefaultRenderScenario.h"

namespace rei::render
{
    SET_LOG_SCOPE("RENDERER")

    void Renderer::SetCamera(const ecs::RefComponent<Camera>& camera)
    {
        _camera = camera;
        if (_renderScenario != nullptr)
        {
            _renderScenario->SetCamera(_camera);
        }

        if (_target)
        {
            int windowWidth;
            int windowHeight;
            glfwGetWindowSize(_target, &windowWidth, &windowHeight);
            camera.Get().SetOutputSize(windowWidth, windowHeight);
        }
    }

    ecs::RefComponent<Camera> Renderer::GetCamera() const
    {
        return _camera;
    }

    void Renderer::SetTarget(GLFWwindow* target)
    {
        _target = target;
        glfwMakeContextCurrent(_target);

        if (target == nullptr) return;

        if (!gladLoadGLLoader(reinterpret_cast<GLADloadproc>(glfwGetProcAddress)))
        {
            REI_THROW("GLAD Initialization failed")
        }

        PrepareMaterials();
        _renderScenario = std::make_unique<DefaultRenderScenario>(_target);
        //_renderScenario = CREATE_RENDER_SCENARIO(_target);
        _renderScenario->Setup();
        
        if (!_camera.IsNull())
        {
            _renderScenario->SetCamera(_camera);
        }
    }

    void Renderer::Render() const
    {
        if (_target == nullptr) return;
        if (!_renderScenario->IsCameraSet())
        {
            LOG("No active camera found...")
            _renderScenario->Clear();
            return;
        }

        _renderScenario->OnBeforeRender();
        _renderScenario->Render();
    }

    void Renderer::Dispose()
    {
        SetTarget(nullptr);
        _renderScenario->Dispose();
    }

    void Renderer::PrepareMaterials() const
    {
        const auto fallbackShader = GetAssetManager().GetById<Shader>(REI_SHADER_SIMPLE_LIT_ASSET_ID);
        auto fallbackMaterial = GetAssetManager().CreateAssetWithId<Material>(REI_FALLBACK_MATERIAL_ID,fallbackShader);
        fallbackMaterial->GetShader().SetFloat("_Shininess", 0.5);
        fallbackMaterial->GetShader().SetColor("_Color", Color(1,1,1,1));

        const auto lightSourceShader = GetAssetManager().GetById<Shader>(REI_SHADER_LIGHT_SOURCE_ASSET_ID);
        GetAssetManager().CreateAssetWithId<Material>(REI_LIGHT_SOURCE_MATERIAL_ID,lightSourceShader);
        
        const auto outlineShader = GetAssetManager().GetById<Shader>(REI_SHADER_ALPHA_OUTLINE_ASSET_ID);
        auto outlineMaterial = GetAssetManager().CreateAssetWithId<Material>(REI_OUTLINE_MATERIAL_ID,outlineShader);
        outlineMaterial->GetShader().SetColor("_Color", Color(1, 0.35f, 0.2f, 1));
        
        const auto overlayTextureShader = GetAssetManager().GetById<Shader>(REI_SHADER_OVERLAY_TEXTURE_ASSET_ID);
        auto overlayTextureMaterial = GetAssetManager().CreateAssetWithId<Material>(REI_OVERLAY_TEXTURE_MATERIAL_ID,overlayTextureShader);
        
        const auto grayscaleShader = GetAssetManager().GetById<Shader>(REI_SHADER_GRAYSCALE_ASSET_ID);
        auto grayscaleMaterial = GetAssetManager().CreateAssetWithId<Material>(REI_OVERLAY_GRAYSCALE_MATERIAL_ID,grayscaleShader);
        
        const auto inversionShader = GetAssetManager().GetById<Shader>(REI_SHADER_INVERSION_ASSET_ID);
        auto inversionMaterial = GetAssetManager().CreateAssetWithId<Material>(REI_OVERLAY_INVERSION_MATERIAL_ID,inversionShader);
        
        const auto depthShader = GetAssetManager().GetById<Shader>(REI_SHADER_DEPTH_ASSET_ID);
        auto depthMaterial = GetAssetManager().CreateAssetWithId<Material>(REI_DEPTH_MATERIAL_ID,depthShader);
        
        const auto colorShader = GetAssetManager().GetById<Shader>(REI_SHADER_COLOR_ASSET_ID);
        auto colorMaterial = GetAssetManager().CreateAssetWithId<Material>(REI_COLOR_MATERIAL_ID,colorShader);
        
        const auto editorGridShader = GetAssetManager().GetById<Shader>(REI_SHADER_EDITOR_GRID_ASSET_ID);
        auto editorGridMaterial = GetAssetManager().CreateAssetWithId<Material>(REI_EDITOR_GRID_MATERIAL_ID,editorGridShader);
    }
}
