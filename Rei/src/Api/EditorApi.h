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

REI_EXTERN_API inline void GetSceneEntityState(const i32 sceneEntityId, char* outputBuffer, const int bufferSize)
{
    const auto& e = rei::GetEntityManager().GetBySceneId(sceneEntityId);
    if (e == rei::ecs::NULL_ENTITY) return;

    nlohmann::json data;
    data["EntityId"] = e.Id;
    data["EntityGeneration"] = e.Generation;

    ECS_WORLD(rei::GetInternalWorld());
    const auto& entityInfo = GET(e, EntityInfo);
    data["SceneId"] = entityInfo.Id;
    data["Name"] = entityInfo.Name;
    data["Behaviours"] = nlohmann::json::array();

    for (const auto behaviour : entityInfo.Behaviours)
    {
        data["Behaviours"].push_back(rei::GetEntityManager().GetBehaviourRegistry().GetBehaviourData(e, behaviour));
    }

    strncpy_s(outputBuffer, bufferSize, data.dump().c_str(), _TRUNCATE);
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
        window = rei::GetEngine().CreateMainWindow();
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
