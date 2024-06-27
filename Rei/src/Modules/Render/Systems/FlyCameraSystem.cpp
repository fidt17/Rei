#include "pch.h"
#include "FlyCameraSystem.h"

#include "../../../../resources/rei_behaviours/render/Camera.h"
#include "../../../../resources/rei_behaviours/transformation/Transform.h"

rei::render::FlyCameraSystem::FlyCameraSystem(
    const std::shared_ptr<ecs::EcsRegistry>& ecs,
    const std::shared_ptr<ecs::FilterProvider>& filters,
    const std::shared_ptr<input::Input>& input)
    : System(ecs, filters),
      _f(filters->Get<transformation::Transform, Camera>()),
      _input(input)
{
}

float deltaTime = 0.0f; // Time between current frame and last frame
float lastFrame = 0.0f; // Time of last frame

float lastX = -1, lastY = -1;
bool didSetCursorPos;

void rei::render::FlyCameraSystem::OnUpdate()
{
    float currentFrame = glfwGetTime();
    deltaTime = currentFrame - lastFrame;
    lastFrame = currentFrame;

    FOR(e, _f)
    {
        auto& transform = GET(e, transformation::Transform);

        const float cameraSpeed = 3.0f * deltaTime; // adjust accordingly

        glm::vec3 cameraRight = transform.GetRight();
        glm::vec3 cameraFront = transform.GetForward();

        if (_input->KeyPressed(GLFW_KEY_W))
        {
            transform.GetPosition() += cameraSpeed * cameraFront;
        }

        if (_input->KeyPressed(GLFW_KEY_S))
        {
            transform.GetPosition() -= cameraSpeed * cameraFront;
        }

        if (_input->KeyPressed(GLFW_KEY_A))
        {
            transform.GetPosition() += cameraSpeed * -cameraRight;
        }

        if (_input->KeyPressed(GLFW_KEY_D))
        {
            transform.GetPosition() += cameraSpeed * cameraRight;
        }

        if (_input->KeyPressed(GLFW_KEY_Q))
        {
            transform.GetRotation().x += cameraSpeed * 45;
        }

        if (_input->KeyPressed(GLFW_KEY_E))
        {
            transform.GetRotation().x -= cameraSpeed * 45;
        }

        if (_input->KeyPressed(GLFW_KEY_R))
        {
            transform.GetRotation().y += cameraSpeed * 45;
        }

        if (_input->KeyPressed(GLFW_KEY_F))
        {
            transform.GetRotation().y -= cameraSpeed * 45;
        }

        f32 xpos, ypos;
        _input->GetCursorPosition(xpos, ypos);
        if (!didSetCursorPos)
        {
            didSetCursorPos = true;
            lastX = xpos;
            lastY = ypos;
        }
        else
        {
            float xoffset = xpos - lastX;
            float yoffset = lastY - ypos; // reversed since y-coordinates range from bottom to top

            const float sensitivity = 0.1f;
            xoffset *= sensitivity;
            yoffset *= sensitivity;
            lastX = xpos;
            lastY = ypos;

            transform.GetRotation().x -= xoffset;
            transform.GetRotation().y += yoffset;

            if (transform.GetRotation().y > 89.0f)
                transform.GetRotation().y = 89.0f;
            if (transform.GetRotation().y < -89.0f)
                transform.GetRotation().y = -89.0f;
        }
    }
}
