#include "pch.h"
#include "FlyCameraSystem.h"

#include "../../../resources/rei_behaviours/render/camera/Camera.h"
#include "../../../resources/rei_behaviours/transformation/Transform.h"
#include "Engine/Engine.h"
#include "Modules/Input/Input.h"
#include "Modules/Window/WindowManager.h"

rei::editor::FlyCameraSystem::FlyCameraSystem(
    const std::shared_ptr<ecs::EcsRegistry>& ecs,
    const std::shared_ptr<ecs::FilterProvider>& filters)
    : System(ecs, filters),
      _f(filters->Get<transformation::Transform, render::Camera>())
{
}

float deltaTime = 0.0f; // Time between current frame and last frame
float lastFrame = 0.0f; // Time of last frame

float lastX = -1, lastY = -1;
bool didSetCursorPos;
int framesToSkip = 5;

void rei::editor::FlyCameraSystem::OnUpdate()
{
    if (Input::IsMouseButtonUp(GLFW_MOUSE_BUTTON_RIGHT))
    {
        didSetCursorPos = false;
    }

    if (!Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_RIGHT)) return;

    double currentFrame = glfwGetTime();
    deltaTime = currentFrame - lastFrame;
    lastFrame = currentFrame;

    FOR(e, _f)
    {
        auto& transform = GET(e, transformation::Transform);

        float cameraSpeed = 3.0f * deltaTime; // adjust accordingly

        glm::vec3 cameraRight = transform.GetRight();
        glm::vec3 cameraFront = transform.GetForward();

        if (Input::IsKeyDown(GLFW_KEY_LEFT_SHIFT))
        {
            cameraSpeed *= 3;
        }

        if (Input::IsKeyDown(GLFW_KEY_W))
        {
            transform.GetPosition() += cameraSpeed * cameraFront;
        }

        if (Input::IsKeyDown(GLFW_KEY_S))
        {
            transform.GetPosition() -= cameraSpeed * cameraFront;
        }

        if (Input::IsKeyDown(GLFW_KEY_A))
        {
            transform.GetPosition() += cameraSpeed * -cameraRight;
        }

        if (Input::IsKeyDown(GLFW_KEY_D))
        {
            transform.GetPosition() += cameraSpeed * cameraRight;
        }

        if (Input::IsKeyDown(GLFW_KEY_Q))
        {
            transform.GetPosition() -= cameraSpeed * cameraRight;
        }

        if (Input::IsKeyDown(GLFW_KEY_E))
        {
            transform.GetPosition() += cameraSpeed * cameraRight;
        }

        if (Input::IsKeyDown(GLFW_KEY_R))
        {
            transform.GetPosition() += math::Vector3::Up() * cameraSpeed;
        }

        if (Input::IsKeyDown(GLFW_KEY_F))
        {
            transform.GetPosition() -= math::Vector3::Up() * cameraSpeed;
        }

        f32 xpos, ypos;
        Input::GetMousePosition(xpos, ypos);
        if (!didSetCursorPos)
        {
            if (xpos >= 0 && ypos >= 0)
            {
                if (framesToSkip-- <= 0)
                {
                    didSetCursorPos = true;
                    lastX = xpos;
                    lastY = ypos;
                }
            }
        }
        else
        {
            constexpr float sensitivity = 0.2f;
            const float xOffset = (xpos - lastX) * sensitivity;
            const float yOffset = (lastY - ypos) * sensitivity; // reversed since y-coordinates range from bottom to top

            lastX = xpos;
            lastY = ypos;


            transform.GetRotation().y += xOffset;
            transform.GetRotation().x -= yOffset;

            if (transform.GetRotation().x > 89.0f)
                transform.GetRotation().x = 89.0f;
            if (transform.GetRotation().x < -89.0f)
                transform.GetRotation().x = -89.0f;

            if (transform.GetRotation().y > 360.0f)
                transform.GetRotation().y = 0.0f;
            if (transform.GetRotation().y < -360.0f)
                transform.GetRotation().y = -0.0f;
        }

        //LOG(std::string(transform))
    }
}
