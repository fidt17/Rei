#include "pch.h"
#include "Camera.h"

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
        view = translate(view, glm::vec3(-_x, -_y, -_z));

        return view;
    }

    float dir = 1;
    void Camera::Update()
    {
        auto time = (float) glfwGetTime();
        auto step = 2;

        _x = cos(time) * step;
        _y = sin(time) * step;
    }
}
