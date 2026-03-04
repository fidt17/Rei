#include "pch.h"
#include "Camera.h"

#include <algorithm>

#include "glm/ext/matrix_clip_space.hpp"
#include "rei_behaviours/transformation/Transform.h"

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

    CameraPerspectiveEnum Camera::GetPerspective() const
    {
        return _perspective;
    }

    void Camera::SetOutputSize(int width, int height)
    {
        _outputWidth = width <= 0 ? 1 : width;
        _outputHeight = height <= 0 ? 1 : height;
    }

    void Camera::SetRenderMode(const RenderMode mode)
    {
        _renderMode = mode;
    }

    void Camera::SetPerspective(const CameraPerspectiveEnum perspective)
    {
        _perspective = perspective;
    }

    glm::mat4 Camera::GetProjectionMatrix() const
    {
        glm::mat4 projection;

        const i32 safeOutputWidth = _outputWidth <= 0 ? 1 : _outputWidth;
        const i32 safeOutputHeight = _outputHeight <= 0 ? 1 : _outputHeight;
        const f32 safeNear = std::max(0.01f, static_cast<f32>(_nearClipPlane) + 0.01f);
        const f32 safeFar = std::max(safeNear + 0.01f, static_cast<f32>(_farClipPlane));
        const f32 safeFov = std::clamp(_fov, 0.01f, 179.0f);
        const f32 aspect = static_cast<float>(safeOutputWidth) / static_cast<float>(safeOutputHeight);
        if (_perspective == Orthographic)
        {
            projection = glm::ortho(-_orthographicSize * aspect, _orthographicSize * aspect,
                                    -_orthographicSize, _orthographicSize,
                                    0.0f, 100.0f);
        }
        else
        {
            projection = glm::perspective(glm::radians(safeFov), aspect, safeNear, safeFar);
        }

        projection = scale(projection, glm::vec3(-1.0f, 1.0f, 1.0f));

        return projection;
    }

    glm::mat4 Camera::GetViewMatrix() const
    {
        auto& transform = GetTransform();
        const auto& position = transform.GetPosition();

        return lookAt(glm::vec3(position), glm::vec3(position + transform.GetForward()), glm::vec3(transform.GetUp()));
    }

    math::Ray Camera::GetScreenPointToRay(const f32 xPos, const f32 yPos) const
    {
        if (_perspective == Perspective)
        {
            return GetPerspectiveScreenPointToRay(xPos, yPos);
        }

        return GetOrhographicScreenPointToRay(xPos, yPos);
    }

    math::Vector3 Camera::WorldToScreenPosition(const math::Vector3& pos) const
    {
        // Transform world point to clip space
        const glm::vec4 clipSpacePos = GetProjectionMatrix() * GetViewMatrix() * glm::vec4(glm::vec3(pos), 1.0f);
    
        // Perspective divide to get normalized device coordinates (NDC)
        const glm::vec3 ndc = glm::vec3(clipSpacePos) / clipSpacePos.w;
    
        // Convert NDC to screen coordinates
        math::Vector3 screenPos;
        screenPos.x = (ndc.x + 1.0f) * 0.5f * static_cast<f32>(_outputWidth);
        screenPos.y = (1.0f - ndc.y) * 0.5f * static_cast<f32>(_outputHeight); // Flip Y-axis
    
        return screenPos;
    }

    f32 Camera::CalculateConstantScale(const math::Vector3& targetPosition, f32 desiredSize) const
    {
        desiredSize = 10 / desiredSize;
        
        if (_perspective == Perspective)
        {
            const glm::vec4 viewSpacePos = GetViewMatrix() * glm::vec4(glm::vec3(targetPosition), 1.0f);
            const f32 distance = -viewSpacePos.z;

            const f32 fovY = 2.0f * atan(1.0f / GetProjectionMatrix()[1][1]);
            const f32 scale = (2.0f * distance * tan(fovY / 2.0f)) / desiredSize;

            return scale;
        }
        
        // For orthographic projection
        float orthoHeight = 2.0f / GetProjectionMatrix()[1][1];
        return orthoHeight / desiredSize;
    }

    ecs::RefComponent<Camera> Camera::GetMainCamera()
    {
        ECS_WORLD(GetInternalWorld());
        const auto mainCameraFilter = FILTER(Camera);

        FOR(e, mainCameraFilter)
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
