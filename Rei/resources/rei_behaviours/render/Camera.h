#pragma once
#include "glm/ext/matrix_transform.hpp"

namespace rei::render
{
    class Camera : public Behaviour
    {
    private:
        BEHAVIOUR_BODY(Camera)
        SERIALIZE f32 _fov;
        SERIALIZE i32 _nearClipPlane;
        SERIALIZE i32 _farClipPlane;

        i32 _outputWidth;
        i32 _outputHeight;

    public:
        f32 GetFov() const;
        i32 GetNearClipPlane() const;
        i32 GetFarClipPlane() const;

        void SetOutputSize(int width, int height);

        glm::mat4 GetProjectionMatrix() const;
        glm::mat4 GetViewMatrix() const;
    };
}

EXPORT_COMPONENT(rei::render::Camera)
