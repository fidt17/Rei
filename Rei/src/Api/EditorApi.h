#pragma once
#include "EditorInputEvent.h"
#include "EditorEventsRelay.h"
#include "Engine/Engine.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"

typedef void (*LogCallbackDelegate)(const rei::common::logging::LogMessage& msg);
REI_EXTERN_API inline void AddLogCallback(const LogCallbackDelegate callback)
{
    rei::common::logging::Log::GetLogger()->NewLogEvent.append([=](const rei::common::logging::LogMessage& msg)
    {
        callback(msg);
    });
}

typedef void (*EngineStartCallbackDelegate)();
REI_EXTERN_API inline void AddEngineStartCallback(const EngineStartCallbackDelegate callback)
{
    rei::GetEngine().StartEvent.append([=]
    {
        callback();
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

typedef void (*EditorInputCallbackDelegate)(const rei::api::EditorInputEvent& inputEvent);
REI_EXTERN_API inline void AddEditorInputCallback(const EditorInputCallbackDelegate callback)
{
    rei::GetEditorEventsRelay().EditorInputReceivedEvent.append([=](const rei::api::EditorInputEvent& inputEvent)
    {
        callback(inputEvent);
    });
}

REI_EXTERN_API inline i64 BuildAsset(const char* file, const char* dest, const i64 offset)
{
    return rei::resources::AssetBuilder().BuildAsset(file, dest, offset);
}

REI_EXTERN_API inline rei::window::Window* CreateEngineWindow()
{
    std::shared_ptr<rei::window::Window> window;

    rei::GetEngine().ExecuteOnMainThread([&]
    {
        WindowCreationSettings windowSettings;
        windowSettings.Name = "Engine Window";
        windowSettings.Width = 100;
        windowSettings.Height = 100;
        windowSettings.HideOnCreation = true;
        windowSettings.CenterCursor = false;
        windowSettings.HideCursor = false;

        window = rei::GetEngine().CreateMainWindow(windowSettings);
        window->DisableStyle();
    })->WaitForCompletion();

    return window.get();
}

REI_EXTERN_API inline HWND GetWindowHandle(const rei::window::Window* window)
{
    return window->GetWindowHandle();
}

REI_EXTERN_API inline void ChangeRenderMode(i32 modeInt)
{
    rei::GetEngine().ExecuteOnMainThread([=]
    {
        const auto mode = static_cast<RenderMode>(modeInt);
        ECS_WORLD(rei::GetInternalWorld());
        const auto& cameraFilter = FILTER(rei::render::Camera);

        FOR(e, cameraFilter)
        {
            GET(e, rei::render::Camera).SetRenderMode(mode);
        }
    });
}

REI_EXTERN_API inline void ChangeTransformationMode(const bool worldSpace)
{
    rei::GetEngine().ExecuteOnMainThread([=]
    {
        ECS_WORLD(rei::GetInternalWorld());
        const auto& controlFilter = FILTER(rei::editor::TransformationControl);

        FOR(e, controlFilter)
        {
            GET(e, rei::editor::TransformationControl).UseWorldSpace = worldSpace;
        }
    });
}

REI_EXTERN_API inline void ResizeWindow(const rei::window::Window* window, const int width, const int height)
{
    if (window == nullptr) return;
    if (!rei::GetEngine().IsRunning()) return;

    rei::GetEngine().ExecuteOnMainThread([window, width, height]
    {
        if (!rei::GetEngine().IsRunning()) return;
        window->Resize(width, height);
    });
}

REI_EXTERN_API inline void SetEditorGridSettings(const rei::render::GridRenderSettings* settings)
{
    rei::GetEngine().ExecuteOnMainThread([=]
    {
        rei::GetEditorEventsRelay().GridRenderSettingsReceivedEvent(*settings);
    });
}
