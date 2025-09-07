#pragma once
#include "rei_behaviours/render/camera/Camera.h"

namespace rei::render
{
    class CameraModule
    {
    public:
        void OnBeforeRender();

        const glm::mat4& GetProjectionMatrix() const;
        const glm::mat4& GetViewMatrix() const;
        i32 GetWidth() const;
        i32 GetHeight() const;

        Color GetBackgroundColor() const;

        void SetCamera(const ecs::RefComponent<Camera>& camera);

        ecs::RefComponent<Camera>& GetCamera();

    private:
        glm::mat4 _projectionMatrix = 0;
        glm::mat4 _viewMatrix = 0;
        i32 _outputWidth = 0, _outputHeight = 0;

        ecs::RefComponent<Camera> _camera;
    };
}
