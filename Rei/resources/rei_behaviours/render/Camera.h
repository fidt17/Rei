#pragma once
#include "glm/ext/matrix_transform.hpp"

namespace rei::render
{
    class Camera : public Behaviour
    {
    private:
        BEHAVIOUR_BODY(Camera)
        SERIALIZE f32 _fov = 45;
        SERIALIZE i32 _nearClipPlane = 0;
        SERIALIZE i32 _farClipPlane = 1000;

        i32 _outputWidth;
        i32 _outputHeight;

    public:
        REI_API f32 GetFov() const;
        REI_API i32 GetNearClipPlane() const;
        REI_API i32 GetFarClipPlane() const;

        REI_API void SetOutputSize(int width, int height);

        REI_API glm::mat4 GetProjectionMatrix() const;
        REI_API glm::mat4 GetViewMatrix() const;
    };
}

EXPORT_COMPONENT(rei::render::Camera)
