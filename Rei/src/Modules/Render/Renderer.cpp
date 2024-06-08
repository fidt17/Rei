#include "Renderer.h"
#include "glfw/glfw3.h"

namespace rei::render
{
    SET_LOG_SCOPE("RENDERER")

    void Renderer::SetTarget(GLFWwindow* target)
    {
        LOG(target == nullptr ? "Reset render target" : "Set render target")

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
}
