#include "pch.h"
#include "CameraModule.h"

void rei::render::CameraModule::OnBeforeRender()
{
    _projectionMatrix = _camera.Get().GetProjectionMatrix();
    _viewMatrix = _camera.Get().GetViewMatrix();
    _camera.Get().GetOutputSize(_outputWidth, _outputHeight);
}

const glm::mat4& rei::render::CameraModule::GetProjectionMatrix() const
{
    return _projectionMatrix;
}

const glm::mat4& rei::render::CameraModule::GetViewMatrix() const
{
    return _viewMatrix;
}

i32 rei::render::CameraModule::GetWidth() const
{
    return _outputWidth;
}

i32 rei::render::CameraModule::GetHeight() const
{
    return _outputHeight;
}

rei::render::Color rei::render::CameraModule::GetBackgroundColor() const
{
    return _camera.Get().GetBackgroundColor();
}

void rei::render::CameraModule::SetCamera(const ecs::RefComponent<Camera>& camera)
{
    _camera = camera;
}
