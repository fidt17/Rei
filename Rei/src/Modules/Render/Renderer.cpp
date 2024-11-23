#include "Renderer.h"
#include "../../../tests/render/BaseRenderScenario.h"

#define RENDER_SCENARIO_NUM 10

#if RENDER_SCENARIO_NUM == 0
    #include "../../../tests/render/hello_triangle/hello_triangle.h"
    #define CREATE_RENDER_SCENARIO(TARGET) std::make_unique<hello_triangle>(TARGET)
#elif RENDER_SCENARIO_NUM == 1
    #include "../../../tests/render/hello_triangle/hello_triangle_indexed.h"
    #define CREATE_RENDER_SCENARIO(TARGET) std::make_unique<hello_triangle_indexed>(TARGET)
#elif RENDER_SCENARIO_NUM == 2
    #include "../../../tests/render/hello_triangle/hello_triangle_e1.h"
    #define CREATE_RENDER_SCENARIO(TARGET) std::make_unique<hello_triangle_e1>(TARGET)
#elif RENDER_SCENARIO_NUM == 3
    #include "../../../tests/render/hello_triangle/hello_triangle_e2.h"
    #define CREATE_RENDER_SCENARIO(TARGET) std::make_unique<hello_triangle_e2>(TARGET)
#elif RENDER_SCENARIO_NUM == 4
    #include "../../../tests/render/hello_triangle/hello_triangle_e3.h"
    #define CREATE_RENDER_SCENARIO(TARGET) std::make_unique<hello_triangle_e3>(TARGET)
#elif RENDER_SCENARIO_NUM == 5
    #include "../../../tests/render/textures/texture_e0.h"
    #define CREATE_RENDER_SCENARIO(TARGET) std::make_unique<texture_e0>(TARGET)
#elif RENDER_SCENARIO_NUM == 6
    #include "../../../tests/render/textures/texture_e1.h"
    #define CREATE_RENDER_SCENARIO(TARGET) std::make_unique<texture_e1>(TARGET)
#elif RENDER_SCENARIO_NUM == 7
    #include "../../../tests/render/transform/transform_e0.h"
    #define CREATE_RENDER_SCENARIO(TARGET) std::make_unique<transform_e0>(TARGET)
#elif RENDER_SCENARIO_NUM == 8
    #include "../../../tests/render/transform/transform_e1.h"
    #define CREATE_RENDER_SCENARIO(TARGET) std::make_unique<transform_e1>(TARGET)
#elif RENDER_SCENARIO_NUM == 9 
    #include "../../../tests/render/light/light_e0.h"
    #define CREATE_RENDER_SCENARIO(TARGET) std::make_unique<light_e0>(TARGET)
#elif RENDER_SCENARIO_NUM == 10 
    #include "../../../tests/render/model/model_e0.h"
    #define CREATE_RENDER_SCENARIO(TARGET) std::make_unique<model_e0>(TARGET)
#endif

namespace rei::render
{
    SET_LOG_SCOPE("RENDERER")

    std::unique_ptr<BaseRenderScenario> _renderScenario;

    void Renderer::SetCamera(const ecs::RefComponent<Camera>& camera)
    {
        _camera = camera;
        if (_renderScenario != nullptr)
        {
            _renderScenario->SetCamera(_camera);
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

        _renderScenario = CREATE_RENDER_SCENARIO(_target);
        _renderScenario->Setup();
        if (!_camera.IsNull())
        {
            _renderScenario->SetCamera(_camera);
        }
    }

    void Renderer::Render() const
    {
        if (_target == nullptr) return;

        _renderScenario->Render();
    }

    void Renderer::Dispose()
    {
        SetTarget(nullptr);
        _renderScenario->Dispose();
    }
}
