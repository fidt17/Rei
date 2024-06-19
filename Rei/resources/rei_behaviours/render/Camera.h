#pragma once
#include "glm/ext/matrix_transform.hpp"

namespace rei::render
{
    class Camera : public Behaviour
    {
    private:
        BEHAVIOUR_BODY(Camera)
        SERIALIZED f32 _fov;
        SERIALIZED i32 _nearClipPlane;
        SERIALIZED i32 _farClipPlane;

        SERIALIZED f32 _x;
        SERIALIZED f32 _y;
        SERIALIZED f32 _z;

        i32 _outputWidth;
        i32 _outputHeight;

    public:
        f32 GetFov() const;
        i32 GetNearClipPlane() const;
        i32 GetFarClipPlane() const;

        void SetOutputSize(int width, int height);

        glm::mat4 GetProjectionMatrix() const;
        glm::mat4 GetViewMatrix() const;

        REI_API void Update() override;
    };
}

EXPORT_COMPONENT(rei::render::Camera)
