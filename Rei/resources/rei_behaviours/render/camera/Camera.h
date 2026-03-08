#pragma once
#include "CameraPerspectiveEnum.h"
#include "Common/Math/Ray.h"
#include "glm/ext/matrix_transform.hpp"
#include "Modules/Render/Color/Color.h"
#include "Modules/Render/RenderScenario/RenderMode.h"

namespace rei::render
{
    class Camera : public Behaviour
    {
    private:
        BEHAVIOUR_BODY(Camera)
        SERIALIZE f32 _fov = 45;
        SERIALIZE f32 _orthographicSize = 4;

        SERIALIZE i32 _nearClipPlane = 0;
        SERIALIZE i32 _farClipPlane = 1000;
        SERIALIZE Color _backgroundColor = Color(19 / 255.0f, 23 / 255.0f, 30 / 255.0f, 1);

        SERIALIZE CameraPerspectiveEnum _perspective = Perspective;

        i32 _outputWidth = 1;
        i32 _outputHeight = 1;

        RenderMode _renderMode = Shaded;

    public:
        REI_API f32 GetFov() const;
        REI_API i32 GetNearClipPlane() const;
        REI_API i32 GetFarClipPlane() const;
        
        REI_API const Color& GetBackgroundColor() const;
        REI_API RenderMode GetRenderMode() const;
        REI_API void GetOutputSize(i32& width, i32& height) const;
        REI_API CameraPerspectiveEnum GetPerspective() const;

        REI_API void SetOutputSize(i32 width, i32 height);
        REI_API void SetRenderMode(RenderMode mode);
        REI_API void SetPerspective(CameraPerspectiveEnum perspective);

        REI_API glm::mat4 GetProjectionMatrix() const;
        REI_API glm::mat4 GetViewMatrix() const;

        REI_API math::Ray GetScreenPointToRay(f32 xPos, f32 yPos) const;
        REI_API math::Vector3 WorldToScreenPosition(const math::Vector3& pos) const;
        REI_API f32 CalculateConstantScale(const math::Vector3& targetPosition, f32 desiredSize) const;

        REI_API static ecs::RefComponent<Camera> GetMainCamera();

    private:
        REI_API math::Ray GetPerspectiveScreenPointToRay(f32 xPos, f32 yPos) const;
        REI_API math::Ray GetOrhographicScreenPointToRay(f32 xPos, f32 yPos) const;
    };
}

EXPORT_COMPONENT(rei::render::Camera)
