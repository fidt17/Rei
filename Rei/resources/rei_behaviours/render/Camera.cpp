#include "pch.h"
#include "Camera.h"

#include "../transformation/Transform.h"
#include "Engine/Services.h"
#include "glfw/glfw3.h"
#include "glm/ext/matrix_clip_space.hpp"
#include "glm/ext/quaternion_common.hpp"
#include "Modules/Input/Input.h"

namespace rei::render
{
    f32 Camera::GetFov() const
    {
        return _fov;
    }

    i32 Camera::GetNearClipPlane() const
    {
        return _nearClipPlane;
    }

    i32 Camera::GetFarClipPlane() const
    {
        return _farClipPlane;
    }

    const Color& Camera::GetBackgroundColor() const
    {
        return _backgroundColor;
    }

    void Camera::SetOutputSize(int width, int height)
    {
        _outputWidth = width;
        _outputHeight = height;
    }

    glm::mat4 Camera::GetProjectionMatrix() const
    {
        f32 aspect = _outputWidth / static_cast<float>(_outputHeight);
        
        return glm::perspective(glm::radians(float(_fov)), aspect, _nearClipPlane + 0.01f, float(_farClipPlane));
    }

    glm::mat4 Camera::GetViewMatrix() const
    {
        glm::vec3 cameraPosition = GetTransform().GetPosition();
        
        glm::vec3 cameraTarget = glm::vec3(0.0f, 0.0f, 0.0f);
        glm::vec3 cameraDirection = glm::normalize(cameraPosition - cameraTarget);

        glm::vec3 up = math::Vector3::Up();
        glm::vec3 cameraRight = glm::normalize(glm::cross(up, cameraDirection));

        glm::vec3 cameraUp = glm::cross(cameraDirection, cameraRight);

        glm::vec3 forward = GetTransform().GetForward();
        
        glm::mat4 view = glm::lookAt(cameraPosition,
                                     cameraPosition + forward,
                                     up);

        return view;
    }
}
