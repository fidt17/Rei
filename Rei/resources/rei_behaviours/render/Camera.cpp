#include "pch.h"
#include "Camera.h"

#include "glfw/glfw3.h"
#include "glm/ext/matrix_clip_space.hpp"
#include "glm/ext/quaternion_common.hpp"

namespace rei::render
{
    i32 Camera::GetFov() const
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

    float fovFloat;
    glm::mat4 Camera::GetProjectionMatrix() const
    {
        f32 aspect = _outputWidth / static_cast<float>(_outputHeight);
        return glm::perspective(glm::radians(float(fovFloat)), aspect, _nearClipPlane + 0.01f, float(_farClipPlane));
    }

    float dir = 1;
    void Camera::Update()
    {
        const f32 from = 20;
        const f32 to = 60;

        fovFloat += dir * 1.f;

        if (fovFloat > to || fovFloat < from)
        {
            dir *= -1;

            if (fovFloat < from) fovFloat = from;
            else if (fovFloat > to) fovFloat = to;
        }
    }
}
