#include "pch.h"
#include "Camera.h"

#include "../transformation/Transform.h"
#include "glfw/glfw3.h"
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
        auto view = glm::mat4(1.0f);
        const auto& position = GetTransform().GetPosition();
        view = translate(view, glm::vec3(-position.X, -position.Y, position.Z));

        return view;
    }
}
