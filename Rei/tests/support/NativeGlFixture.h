#pragma once
#include "NativeTestSupport.h"
#include "glad/glad.h"
#include "GLFW/glfw3.h"
#include <array>

namespace rei::tests
{
    // Real driver/context. Objects using GL must be destroyed before this fixture.
    class NativeGlFixture
    {
    public:
        explicit NativeGlFixture(const i32 width = 32, const i32 height = 32)
        {
            if (!IsIsolatedChild()) throw std::logic_error("NativeGlFixture requires an isolated native test child");
            if (!glfwInit()) throw std::runtime_error("GL prerequisite unavailable: glfwInit failed");
            glfwDefaultWindowHints();
            glfwWindowHint(GLFW_VISIBLE, GLFW_FALSE);
            glfwWindowHint(GLFW_DECORATED, GLFW_FALSE); // Windows decorated windows impose a minimum client width.
            glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 3);
            glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 3);
            glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);
            glfwWindowHint(GLFW_SAMPLES, 0);
            _window = glfwCreateWindow(width, height, "Rei native GL tests", nullptr, nullptr);
            if (!_window)
            {
                glfwTerminate();
                throw std::runtime_error("GL prerequisite unavailable: hidden OpenGL 3.3 context creation failed");
            }
            glfwMakeContextCurrent(_window);
            if (!gladLoadGLLoader(reinterpret_cast<GLADloadproc>(glfwGetProcAddress)))
            {
                glfwDestroyWindow(_window);
                glfwTerminate();
                throw std::runtime_error("GL prerequisite unavailable: GLAD initialization failed");
            }
            glfwSwapInterval(0);
            glDisable(GL_DITHER);
            glDisable(GL_MULTISAMPLE);
            glViewport(0, 0, width, height);
        }

        ~NativeGlFixture()
        {
            glfwMakeContextCurrent(nullptr);
            glfwDestroyWindow(_window);
            glfwTerminate();
        }

        NativeGlFixture(const NativeGlFixture&) = delete;
        NativeGlFixture& operator=(const NativeGlFixture&) = delete;
        GLFWwindow* Window() const { return _window; }

    private:
        GLFWwindow* _window = nullptr;
    };

    inline void IsolatedGl(const std::function<void()>& body) { Isolated(body, 15000, 1024); }

    inline std::array<u8, 4> ReadPixel(const i32 x = 16, const i32 y = 16)
    {
        std::array<u8, 4> result{};
        glReadPixels(x, y, 1, 1, GL_RGBA, GL_UNSIGNED_BYTE, result.data());
        return result;
    }

    inline void RequirePixel(const std::array<u8, 4>& actual, const std::array<u8, 4>& expected, const i32 tolerance = 1)
    {
        INFO("RGBA actual " << static_cast<i32>(actual[0]) << "," << static_cast<i32>(actual[1]) << "," << static_cast<i32>(actual[2]) << "," << static_cast<i32>(actual[3]));
        for (u32 i = 0; i < actual.size(); ++i) CHECK(std::abs(static_cast<i32>(actual[i]) - static_cast<i32>(expected[i])) <= tolerance);
    }
}
