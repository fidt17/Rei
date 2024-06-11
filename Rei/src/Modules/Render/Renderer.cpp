#include "Renderer.h"
#include "../../../tests/render/BaseRenderScenario.h"

#define RENDER_SCENARIO_NUM 5

#if RENDER_SCENARIO_NUM == 0
    #include "../../../tests/render/hello_triangle/hello_triangle.h"
    #define CREATE_RENDER_SCENARIO(TARGET) std::make_shared<hello_triangle>(TARGET)
#elif RENDER_SCENARIO_NUM == 1
    #include "../../../tests/render/hello_triangle/hello_triangle_indexed.h"
    #define CREATE_RENDER_SCENARIO(TARGET) std::make_shared<hello_triangle_indexed>(TARGET)
#elif RENDER_SCENARIO_NUM == 2
    #include "../../../tests/render/hello_triangle/hello_triangle_e1.h"
    #define CREATE_RENDER_SCENARIO(TARGET) std::make_shared<hello_triangle_e1>(TARGET)
#elif RENDER_SCENARIO_NUM == 3
    #include "../../../tests/render/hello_triangle/hello_triangle_e2.h"
    #define CREATE_RENDER_SCENARIO(TARGET) std::make_shared<hello_triangle_e2>(TARGET)
#elif RENDER_SCENARIO_NUM == 4
    #include "../../../tests/render/hello_triangle/hello_triangle_e3.h"
    #define CREATE_RENDER_SCENARIO(TARGET) std::make_shared<hello_triangle_e3>(TARGET)
#elif RENDER_SCENARIO_NUM == 5
    #include "../../../tests/render/shader_ex//shader_e0.h"
    #define CREATE_RENDER_SCENARIO(TARGET) std::make_shared<shader_e0>(TARGET)
#endif

namespace rei::render
{
    SET_LOG_SCOPE("RENDERER")

    std::shared_ptr<BaseRenderScenario> _renderScenario;

    Renderer::Renderer()
    {
        glfwSetErrorCallback([](int error_code, const char* description)
        {
            LOG_ERROR("GLFW ERROR. " + STRING(error_code) + " " + description);
        });

        if (!glfwInit())
        {
            REI_THROW("GLFW Initialization error")
        }

        glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 3);
        glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 3);
        glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);
    }

    void Renderer::SetTarget(GLFWwindow* target)
    {
        LOG(target == nullptr ? "Reset render target" : "Set render target")

        _target = target;
        glfwMakeContextCurrent(_target);

        if (target == nullptr) return;

        if (!gladLoadGLLoader(reinterpret_cast<GLADloadproc>(glfwGetProcAddress)))
        {
            REI_THROW("GLAD Initialization failed")
        }

        _renderScenario = CREATE_RENDER_SCENARIO(_target);
        _renderScenario->Setup();
        LOG("Setup render scenario complete")
    }

    void Renderer::Render()
    {
        if (_target == nullptr) return;

        _renderScenario->Render();
    }

    void Renderer::Dispose()
    {
        glfwTerminate();
    }
}
