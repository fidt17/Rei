#include "Renderer.h"

#include "GLFW/glfw3.h"

namespace rei::render
{
    static void key_callback(GLFWwindow* window, int key, int scancode, int action, int mods)
    {
        /*
        if (key == GLFW_KEY_ESCAPE && action == GLFW_PRESS)
        {
            glfwSetWindowShouldClose(window, GLFW_TRUE);
        }
        */

        LOG("key: " + STRING(key) + " scancode: " + STRING(scancode) + " " + STRING(action) + " " + STRING(mods))
    }

    void Renderer::SetupWindow(const int width, const int height, const std::string& name)
    {
        if (!glfwInit())
            REI_THROW("GLFW Initialization error")

        _window = glfwCreateWindow(width, height, name.c_str(), nullptr, nullptr);
        if (!_window)
        {
            glfwTerminate();
            REI_THROW("Could not create window." + name)
        }

        glfwSetKeyCallback(_window, key_callback);

        // set window style
        //SetWindowLongPtr(glfwGetWin32Window(_window), GWL_STYLE, 0);
    }

    void Renderer::Render()
    {
        if (_window == nullptr) return;
        if (glfwWindowShouldClose(_window)) return;

        glfwMakeContextCurrent(_window);

        glClear(GL_COLOR_BUFFER_BIT);

        glClearColor(r += rSpeed, g += gSpeed, b += bSpeed, 1);

        if (r > 1 || r < 0) rSpeed *= -1;
        if (g > 1 || g < 0) gSpeed *= -1;
        if (b > 1 || b < 0) bSpeed *= -1;

        glBegin(GL_TRIANGLES);
        glVertex2f(-b, -1);
        glVertex2f(0, r);
        glVertex2f(g, -1);
        glEnd();

        /* Swap front and back buffers */
        glfwSwapBuffers(_window);

        /* Poll for and process events */
        glfwPollEvents();
    }

    void Renderer::Terminate()
    {
        _window = nullptr;
        glfwTerminate();
    }

    HWND Renderer::GetWindowHandler() const
    {
        REI_ASSERT_NOT_NULL(_window)

        return glfwGetWin32Window(_window);
    }
}
