#include "pch.h"
#include "FlyCameraSystem.h"

#include "glm/ext/quaternion_trigonometric.hpp"
#include "Modules/Input/Input.h"
#include "Modules/Window/WindowManager.h"
#include "rei_behaviours/render/camera/Camera.h"
#include "rei_behaviours/transformation/Transform.h"

namespace rei::editor
{
    FlyCameraSystem::FlyCameraSystem(const std::shared_ptr<ecs::World>& world) : System(world)
    {
        _cameraFilter = FILTER(Transform, render::Camera);
    }

    f32 deltaTime = 0.0f; // Time between current frame and last frame
    f32 lastFrame = 0.0f; // Time of last frame

    f32 lastX = -1, lastY = -1;
    bool didSetCursorPos;
    i32 framesToSkip = 5;

    void FlyCameraSystem::MoveCamera(Transform& transform, const f32 cameraSpeed) const
    {
        const math::Vector3 cameraRight = transform.GetRight();
        const math::Vector3 cameraFront = transform.GetForward();

        if (Input::IsKeyDown(GLFW_KEY_W))
        {
            transform.Translate((cameraFront * cameraSpeed));
        }

        if (Input::IsKeyDown(GLFW_KEY_S))
        {
            transform.Translate(-(cameraFront * cameraSpeed));
        }

        if (Input::IsKeyDown(GLFW_KEY_A))
        {
            transform.Translate((-cameraRight * cameraSpeed));
        }

        if (Input::IsKeyDown(GLFW_KEY_D))
        {
            transform.Translate((cameraRight * cameraSpeed));
        }

        if (Input::IsKeyDown(GLFW_KEY_Q))
        {
            transform.Translate(-(cameraRight * cameraSpeed));
        }

        if (Input::IsKeyDown(GLFW_KEY_E))
        {
            transform.Translate((cameraRight * cameraSpeed));
        }

        if (Input::IsKeyDown(GLFW_KEY_R))
        {
            transform.Translate((math::Vector3::Up() * cameraSpeed));
        }

        if (Input::IsKeyDown(GLFW_KEY_F))
        {
            transform.Translate(-(math::Vector3::Up() * cameraSpeed));
        }
    }

    void FlyCameraSystem::RotateCamera(Transform& transform) const
    {
        f32 pointerXPos, pointerYPos;
        Input::GetMousePosition(pointerXPos, pointerYPos);
        if (!didSetCursorPos)
        {
            if (pointerXPos >= 0 && pointerYPos >= 0)
            {
                if (framesToSkip-- <= 0)
                {
                    didSetCursorPos = true;
                    lastX = pointerXPos;
                    lastY = pointerYPos;
                }
            }
        }
        else
        {
            constexpr f32 sensitivity = 0.2f;
            const f32 xOffset = (pointerXPos - lastX) * sensitivity;
            const f32 yOffset = (lastY - pointerYPos) * sensitivity; // reversed since y-coordinates range from bottom to top

            lastX = pointerXPos;
            lastY = pointerYPos;

            transform.RotateLocal(-yOffset, {1, 0, 0});
            transform.RotateWorld(xOffset, {0, 1, 0});
        }
    }

    void FlyCameraSystem::OnUpdate()
    {
        if (Input::IsMouseButtonUp(GLFW_MOUSE_BUTTON_RIGHT))
        {
            didSetCursorPos = false;
        }

        f64 currentFrame = glfwGetTime();
        deltaTime = currentFrame - lastFrame;
        lastFrame = currentFrame;

        FOR(e, _cameraFilter)
        {
            auto& transform = GET(e, Transform);

            f32 cameraSpeed = 3.0f * deltaTime;

            if (Input::IsKeyDown(GLFW_KEY_LEFT_SHIFT))
            {
                cameraSpeed *= 3;
            }

            if (Input::IsKeyReleased(GLFW_KEY_P))
            {
                auto& camera = GET(e, render::Camera);
                camera.SetPerspective(camera.GetPerspective() == render::Perspective ? render::Orthographic : render::Perspective);
            }

            if (!Input::IsMouseButtonDown(GLFW_MOUSE_BUTTON_RIGHT)) return;

            MoveCamera(transform, cameraSpeed);
            RotateCamera(transform);
        }
    }
}
