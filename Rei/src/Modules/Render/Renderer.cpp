#include "Renderer.h"

#include "glfw/glfw3.h"

namespace rei::render
{
    void Renderer::SetTarget(GLFWwindow* target)
    {
        _target = target;
    }

    void Renderer::Render()
    {
        if (_target == nullptr) return;
        
        glfwMakeContextCurrent(_target);

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

        glfwSwapBuffers(_target);
    }

    HWND Renderer::GetWindowHandler() const
    {
        REI_ASSERT_NOT_NULL(_target)
        return glfwGetWin32Window(_target);
    }
}
