#pragma once
#include "EditorInputEvent.h"
#include "ViewportInputApi.h"
#include "EditorEventsRelay.h"
#include "SceneSyncRelay.h"
#include <future>
#include "Modules/Scenes/SceneManager.h"
#include "Engine/Engine.h"
#include "Modules/Editor/TransformationControls/TransformationControl.h"

typedef void (*LogCallbackDelegate)(const rei::common::logging::LogMessage& msg);
REI_EXTERN_API inline void AddLogCallback(const LogCallbackDelegate callback)
{
    rei::common::logging::Log::GetLogger()->AddCallback(callback);
}

typedef void (*EngineStartCallbackDelegate)();
REI_EXTERN_API inline void AddEngineStartCallback(const EngineStartCallbackDelegate callback)
{
    rei::GetEngine().StartEvent.append([=]
    {
        callback();
    });
}

typedef void (*SceneLoadProgressCallbackDelegate)(i32 percent);
REI_EXTERN_API inline void AddSceneLoadProgressCallback(const SceneLoadProgressCallbackDelegate callback)
{
    if (callback == nullptr) return;
    rei::GetSceneManager().LoadProgressEvent.append([=](const i32 percent) { callback(percent); });
}

typedef void (*ShutdownCallbackDelegate)(i32 exitCode);
REI_EXTERN_API inline void AddShutdownCallback(const ShutdownCallbackDelegate callback)
{
    rei::GetEngine().ShutdownEvent.append([=](const i32 exitCode)
    {
        callback(exitCode);
    });
}

typedef void (*SceneSyncCallbackDelegate)(const char* json, i32 byteCount);
REI_EXTERN_API inline void SetSceneSyncCallback(const SceneSyncCallbackDelegate callback)
{
    rei::GetSceneSyncRelay().SetCallback(callback);
}

REI_EXTERN_API inline void RequestSceneSync()
{
    rei::GetEngine().ExecuteOnMainThread([] { rei::GetSceneSyncRelay().RequestSnapshot(); });
}

REI_EXTERN_API inline i32 LoadEditorScene(const char* requestJson)
{
    if (requestJson == nullptr || !rei::GetEngine().IsRunning()) return 0;
    auto completion = std::make_shared<std::promise<i32>>();
    auto result = completion->get_future();
    rei::GetEngine().ExecuteOnMainThread([request = std::string(requestJson), completion]
    {
        rei::GetEngine().GetEditorActions().Clear();
        i32 loaded = 0;
        try
        {
            const auto data = nlohmann::json::parse(request);
            if (data.at("Scene").is_null()) rei::GetSceneManager().UnloadCurrentScene();
            else loaded = rei::GetSceneManager().LoadScene(data.at("SceneId").get<std::string>(), data.at("Scene")) ? 1 : 0;
        }
        catch (const std::exception& error)
        {
            rei::GetSceneManager().UnloadCurrentScene();
            LOG_ERROR("Failed to open Editor scene. {}", error.what())
        }
        catch (...)
        {
            rei::GetSceneManager().UnloadCurrentScene();
            LOG_ERROR("Failed to open Editor scene. Unknown exception")
        }
        rei::GetSceneSyncRelay().RequestSnapshot();
        completion->set_value(loaded);
    });
    while (result.wait_for(std::chrono::milliseconds(10)) != std::future_status::ready)
    {
        if (!rei::GetEngine().IsRunning()) return 0;
    }
    return result.get();
}

namespace rei::api
{
    // Called from outside the engine thread, like LoadEditorScene.
    inline i32 ReplayEditorAction(const u64 actionId, const bool undo)
    {
        auto& engine = GetEngine();
        if (!engine.IsRunning() || !engine.IsEditorMode()) return 0;
        auto completion = std::make_shared<std::promise<i32>>();
        auto result = completion->get_future();
        engine.ExecuteOnMainThread([actionId, undo, completion]
        {
            auto replay = editor::EditorActionReplayResult::Failed;
            try
            {
                auto& actions = GetEngine().GetEditorActions();
                replay = undo ? actions.Undo(actionId) : actions.Redo(actionId);
            }
            catch (...) { LOG_ERROR("Failed to replay Editor action {}", actionId) }
            completion->set_value(static_cast<i32>(replay));
        });
        while (result.wait_for(std::chrono::milliseconds(10)) != std::future_status::ready)
        {
            if (!engine.IsRunning()) return 0;
        }
        return result.get();
    }
}

REI_EXTERN_API inline i32 UndoEditorAction(const u64 actionId) { return rei::api::ReplayEditorAction(actionId, true); }
REI_EXTERN_API inline i32 RedoEditorAction(const u64 actionId) { return rei::api::ReplayEditorAction(actionId, false); }

typedef void (*EditorActionRecordedCallbackDelegate)(u64 actionId);
REI_EXTERN_API inline void AddEditorActionRecordedCallback(const EditorActionRecordedCallbackDelegate callback)
{
    if (callback == nullptr) return;
    rei::GetEditorEventsRelay().EditorActionRecordedEvent.append([callback](const u64 id) { callback(id); });
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

typedef void (*FrameCaptureCallbackDelegate)(const u8* pixels, i32 width, i32 height);
REI_EXTERN_API inline i32 RequestFrameCapture(const FrameCaptureCallbackDelegate callback)
{
    if (callback == nullptr || !rei::GetEngine().IsRunning()) return 0;

    return rei::GetEngine().RequestFrameCapture(callback) ? 1 : 0;
}

REI_EXTERN_API inline void ChangeRenderMode(i32 modeInt, const bool isUiRenderingEnabled)
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
        rei::GetEditorEventsRelay().UiRenderingEnabledReceivedEvent(isUiRenderingEnabled);
    });
}

REI_EXTERN_API inline void ChangeTransformationMode(const i32 modeInt, const bool worldSpace)
{
    rei::GetEngine().ExecuteOnMainThread([=]
    {
        const auto mode = static_cast<rei::editor::TransformationMode>(modeInt);
        ECS_WORLD(rei::GetInternalWorld());
        const auto& controlFilter = FILTER(rei::editor::TransformationControl);

        FOR(e, controlFilter)
        {
            auto& control = GET(e, rei::editor::TransformationControl);
            control.Mode = mode;
            control.UseWorldSpace = worldSpace;
        }
    });
}

REI_EXTERN_API inline i32 GetTransformationMode()
{
    if (!rei::GetEngine().IsRunning()) return -1;

    i32 mode = -1;
    rei::GetEngine().ExecuteOnMainThread([&]
    {
        ECS_WORLD(rei::GetInternalWorld());
        const auto& controlFilter = FILTER(rei::editor::TransformationControl);

        FOR(e, controlFilter)
        {
            const auto& control = GET(e, rei::editor::TransformationControl);
            mode = static_cast<i32>(control.Mode);
            return;
        }
    })->WaitForCompletion();

    return mode;
}

REI_EXTERN_API inline void ResizeWindow(const rei::window::Window* window, const i32 width, const i32 height)
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
