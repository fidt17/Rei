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
    return rei::resources::BuildAsset(file, dest, offset);
}

REI_EXTERN_API inline void* CreatePlaymodeWindow()
{
    auto& engine = rei::Services::GetInstance()->GetEngine();
    std::shared_ptr<rei::window::Window> window;

    auto t = std::make_shared<rei::Task>(
        [&]
        {
            window = engine.CreateMainWindow();
        }
    );

    engine.GetMainThread().AddTask(t);
    t->WaitForCompletion();

    return window->GetWindowHandle();
}
