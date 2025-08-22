#pragma once
#include "Engine/Engine.h"
#include "Engine/Services.h"
#include "Modules/Resources/AssetBuilder.h"

typedef void (*LogCallbackDelegate)(const rei::common::logging::LogMessage& msg);
REI_EXTERN_API inline void AddLogCallback(const LogCallbackDelegate callback)
{
    rei::common::logging::Log::GetLogger()->NewLogEvent.append([=](const rei::common::logging::LogMessage& msg)
    {
        callback(msg);
    });
}

typedef void (*ShutdownCallbackDelegate)(int exitCode);
REI_EXTERN_API inline void AddShutdownCallback(const ShutdownCallbackDelegate callback)
{
    rei::GetEngine().ShutdownEvent.append([=](const int exitCode)
    {
        callback(exitCode);
    });
}

REI_EXTERN_API inline i64 BuildAsset(const char* file, const char* dest, const i64 offset)
{
    return rei::resources::AssetBuilder().BuildAsset(file, dest, offset);
}

REI_EXTERN_API inline rei::window::Window* CreatePlaymodeWindow()
{
    std::shared_ptr<rei::window::Window> window;

    rei::GetEngine().ExecuteOnMainThread([&]
    {
        window = rei::GetEngine().CreateMainWindow(1080, 720, true);
        window->DisableStyle();
    })->WaitForCompletion();

    return window.get();
}

REI_EXTERN_API inline HWND GetWindowHandle(const rei::window::Window* window)
{
    return window->GetWindowHandle();
}

REI_EXTERN_API inline void ResizeWindow(const rei::window::Window* window, const int width, const int height)
{
    rei::GetEngine().ExecuteOnMainThread([&]
    {
        window->Resize(width, height);
    })->WaitForCompletion();
}
