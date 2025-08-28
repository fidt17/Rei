#include "pch.h"
#include "Camera.h"

#include "../transformation/Transform.h"
#include "glm/ext/matrix_clip_space.hpp"
#include "glm/ext/quaternion_common.hpp"

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

    RenderMode Camera::GetRenderMode() const
    {
        return _renderMode;
    }

    void Camera::SetOutputSize(int width, int height)
    {
        _outputWidth = width;
        _outputHeight = height;
    }

    void Camera::SetRenderMode(const RenderMode mode)
    {
        _renderMode = mode;
    }

    glm::mat4 Camera::GetProjectionMatrix() const
    {
        const f32 aspect = static_cast<float>(_outputWidth) / static_cast<float>(_outputHeight);
        if (_perspective == Orthographic)
        {
            return glm::ortho(-_orthographicSize * aspect, _orthographicSize * aspect,
                              -_orthographicSize, _orthographicSize,
                              0.0f, 100.0f);
        }

        // else return perspective
        return glm::perspective(glm::radians(_fov), aspect, static_cast<float>(_nearClipPlane) + 0.01f, static_cast<float>(_farClipPlane));
    }

    glm::mat4 Camera::GetViewMatrix() const
    {
        const glm::vec3 cameraPosition = GetTransform().GetPosition();

        constexpr glm::vec3 cameraTarget = glm::vec3(0.0f, 0.0f, 0.0f);
        const glm::vec3 cameraDirection = glm::normalize(cameraPosition - cameraTarget);

        const glm::vec3 up = math::Vector3::Up();
        const glm::vec3 cameraRight = glm::normalize(glm::cross(up, cameraDirection));

        const glm::vec3 cameraUp = glm::cross(cameraDirection, cameraRight);

        const glm::vec3 forward = GetTransform().GetForward();

        const glm::mat4 view = glm::lookAt(cameraPosition,
                                           cameraPosition + forward,
                                           up);

        return view;
    }
}
