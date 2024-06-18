#pragma once
#include "glm/ext/matrix_transform.hpp"

namespace rei::render
{
    class Camera : public Behaviour
    {
    private:
        BEHAVIOUR_BODY(Camera)
        SERIALIZED i32 _fov;
        SERIALIZED i32 _nearClipPlane;
        SERIALIZED i32 _farClipPlane;

        i32 _outputWidth;
        i32 _outputHeight;

    public:
        i32 GetFov() const;
        i32 GetNearClipPlane() const;
        i32 GetFarClipPlane() const;

        void SetOutputSize(int width, int height);

        glm::mat4 GetProjectionMatrix() const;

        REI_API void Update() override;
    };
}

EXPORT_COMPONENT(rei::render::Camera)
