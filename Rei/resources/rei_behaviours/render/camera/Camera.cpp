#include "pch.h"
#include "Camera.h"

#include "Modules/Render/Camera/MainCameraTag.h"
#include "../../transformation/Transform.h"
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

    void Camera::GetOutputSize(int& width, int& height) const
    {
        width = _outputWidth;
        height = _outputHeight;
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
        const glm::vec3 cameraDirection = normalize(cameraPosition - cameraTarget);

        const glm::vec3 up = math::Vector3::Up();
        const glm::vec3 cameraRight = normalize(cross(up, cameraDirection));

        const glm::vec3 cameraUp = cross(cameraDirection, cameraRight);

        const glm::vec3 forward = GetTransform().GetForward();

        const glm::mat4 view = lookAt(cameraPosition,
                                      cameraPosition + forward,
                                      up);

        return view;
    }

    math::Ray Camera::GetScreenPointToRay(const f32 xPos, const f32 yPos) const
    {
        if (_perspective == Perspective)
        {
            return GetPerspectiveScreenPointToRay(xPos, yPos);
        }
        
        return GetOrhographicScreenPointToRay(xPos, yPos);
    }

    ecs::RefComponent<Camera> Camera::GetMainCamera()
    {
        ECS_WORLD(GetInternalWorld());
        auto f = GetInternalWorld().GetFiltersRegistry()->Get<Camera, MainCameraTag>();

        FOR(e, f)
        {
            return GET_REF(e, Camera);
        }

        return {};
    }

    math::Ray Camera::GetPerspectiveScreenPointToRay(const f32 xPos, const f32 yPos) const
    {
        // Convert screen coordinates to normalized device coordinates
        const f32 x = (2.0f * xPos) / _outputWidth - 1.0f;
        const f32 y = 1.0f - (2.0f * yPos) / _outputHeight;

        // Convert to clip coordinates
        const auto rayClip = glm::vec4(x, y, -1.0f, 1.0f);

        // Convert to eye coordinates
        const glm::mat4 projection = GetProjectionMatrix();
        glm::vec4 rayEye = inverse(projection) * rayClip;
        rayEye = glm::vec4(rayEye.x, rayEye.y, -1.0f, 0.0f);

        // Convert to world coordinates
        const glm::mat4 view = GetViewMatrix();
        const glm::vec4 rayWorld = inverse(view) * rayEye;
        const glm::vec3 rayDirection = normalize(glm::vec3(rayWorld));

        return math::Ray(GetTransform().GetPosition(), math::Vector3(rayDirection));
    }

    math::Ray Camera::GetOrhographicScreenPointToRay(const f32 xPos, const f32 yPos) const
    {
        // Convert screen coordinates to normalized device coordinates (NDC)
        float ndcX = (2.0f * xPos) / _outputWidth - 1.0f;
        float ndcY = 1.0f - (2.0f * yPos) / _outputHeight; // Flip Y-axis
        
        // Get view and projection matrices
        const glm::mat4 view = GetViewMatrix();
        const glm::mat4 projection = GetProjectionMatrix();
        
        // Calculate inverse of view-projection matrix
        const glm::mat4 invViewProj = glm::inverse(projection * view);
        
        // Create points at near and far planes in NDC
        const glm::vec4 nearPointNDC(ndcX, ndcY, -1.0f, 1.0f);
        const glm::vec4 farPointNDC(ndcX, ndcY, 1.0f, 1.0f);
        
        // Convert to world space
        const glm::vec4 nearPointWorld = invViewProj * nearPointNDC;
        const glm::vec4 farPointWorld = invViewProj * farPointNDC;
        
        // For orthographic, no perspective division needed
        math::Ray ray;
        ray.Origin = math::Vector3(nearPointWorld);
        ray.Direction = math::Vector3::Normalize(math::Vector3(farPointWorld) - ray.Origin);
        
        return ray;
    }
}
