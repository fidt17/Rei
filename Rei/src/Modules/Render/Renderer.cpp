#include "Renderer.h"

#include <algorithm>
#include <utility>

#include "Material/Material.h"
#include "RenderScenario/DefaultRenderScenario.h"
#include "Textures/Texture.h"

namespace rei::render
{
    Renderer::Renderer(std::vector<std::unique_ptr<CustomRenderModule>> customRenderModules)
        : _customRenderModules(std::move(customRenderModules))
    {
        std::erase_if(_customRenderModules, [](const auto& module) { return module == nullptr; });
    }

    void Renderer::SetCamera(const ecs::ComponentRef<Camera>& camera)
    {
        _camera = camera;
        if (_renderScenario != nullptr)
        {
            _renderScenario->SetCamera(_camera);
        }

        if (_target)
        {
            i32 windowWidth;
            i32 windowHeight;
            glfwGetWindowSize(_target, &windowWidth, &windowHeight);
            camera.Get().SetOutputSize(windowWidth, windowHeight);
        }
    }

    ecs::ComponentRef<Camera> Renderer::GetCamera() const
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

        PrepareAssets();
        _renderScenario = std::make_unique<DefaultRenderScenario>(_target, _customRenderModules);
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
            _renderScenario->RenderWithoutCamera();
            return;
        }

        _renderScenario->OnBeforeRender();
        _renderScenario->Render();
    }

    bool Renderer::RequestFrameCapture(const FrameCaptureCallback& callback) const
    {
        if (_renderScenario == nullptr) return false;
        return _renderScenario->RequestFrameCapture(callback);
    }

    void Renderer::Dispose()
    {
        if (_target != nullptr) glfwMakeContextCurrent(_target);

        if (_renderScenario != nullptr)
        {
            _renderScenario->Dispose();
            _renderScenario.reset();
        }

        _customRenderModules.clear();
        SetTarget(nullptr);
    }

    void Renderer::PrepareAssets() const
    {
        LOG_DEBUG("Preparing engine assets")

        GetAssetManager().CreateAssetWithId<Texture>(
            REI_WHITE_FALLBACK_TEXTURE_ID,
            1,
            1,
            GL_RGBA,
            std::vector<u8> {255, 255, 255, 255});
        
        const auto errorShader = GetAssetManager().GetById<Shader>(REI_SHADER_ERROR_ASSET_ID);
        auto errorMaterial = GetAssetManager().CreateAssetWithId<Material>(REI_ERROR_MATERIAL_ID,errorShader);

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
