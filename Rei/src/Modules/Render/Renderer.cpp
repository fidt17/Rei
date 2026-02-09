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
        // todo: put in engine resources
        //const auto fallbackShader = GetAssetManager().GetByPath<Shader>("C:/Repos/Rei/Rei/resources/shaders/special/error.rshader");
        
        const auto fallbackShader = GetAssetManager().GetByPath<Shader>("C:/Repos/Rei/Rei/resources/shaders/simple_lit.rshader");
        auto fallbackMaterial = GetAssetManager().CreateAssetWithId<Material>(REI_FALLBACK_MATERIAL_ID,fallbackShader);
        fallbackMaterial->GetShader().SetFloat("_Shininess", 0.5);
        fallbackMaterial->GetShader().SetColor("_Color", Color(1,1,1,1));

        const auto lightSourceShader = GetAssetManager().GetByPath<Shader>("C:/Repos/Rei/Rei/resources/shaders/light_source.rshader");
        GetAssetManager().CreateAssetWithId<Material>(REI_LIGHT_SOURCE_MATERIAL_ID,lightSourceShader);
        
        const auto outlineShader = GetAssetManager().GetByPath<Shader>("C:/Repos/Rei/Rei/resources/shaders/post_processing/alpha_outline.rshader");
        auto outlineMaterial = GetAssetManager().CreateAssetWithId<Material>(REI_OUTLINE_MATERIAL_ID,outlineShader);
        outlineMaterial->GetShader().SetColor("_Color", Color(1, 0.35f, 0.2f, 1));
        
        const auto overlayTextureShader = GetAssetManager().GetByPath<Shader>("C:/Repos/Rei/Rei/resources/shaders/post_processing/overlay_texture.rshader");
        auto overlayTextureMaterial = GetAssetManager().CreateAssetWithId<Material>(REI_OVERLAY_TEXTURE_MATERIAL_ID,overlayTextureShader);
        
        const auto grayscaleShader = GetAssetManager().GetByPath<Shader>("C:/Repos/Rei/Rei/resources/shaders/post_processing/grayscale.rshader");
        auto grayscaleMaterial = GetAssetManager().CreateAssetWithId<Material>(REI_OVERLAY_GRAYSCALE_MATERIAL_ID,grayscaleShader);
        
        const auto inversionShader = GetAssetManager().GetByPath<Shader>("C:/Repos/Rei/Rei/resources/shaders/post_processing/inversion.rshader");
        auto inversionMaterial = GetAssetManager().CreateAssetWithId<Material>(REI_OVERLAY_INVERSION_MATERIAL_ID,inversionShader);
        
        const auto depthShader = GetAssetManager().GetByPath<Shader>("C:/Repos/Rei/Rei/resources/shaders/special/depth.rshader");
        auto depthMaterial = GetAssetManager().CreateAssetWithId<Material>(REI_DEPTH_MATERIAL_ID,depthShader);
        
        const auto colorShader = GetAssetManager().GetByPath<Shader>("C:/Repos/Rei/Rei/resources/shaders/color.rshader");
        auto colorMaterial = GetAssetManager().CreateAssetWithId<Material>(REI_COLOR_MATERIAL_ID,colorShader);
        
        const auto editorGridShader = GetAssetManager().GetByPath<Shader>("C:/Repos/Rei/Rei/resources/shaders/editor/editor_grid.rshader");
        auto editorGridMaterial = GetAssetManager().CreateAssetWithId<Material>(REI_EDITOR_GRID_MATERIAL_ID,editorGridShader);
    }
}
