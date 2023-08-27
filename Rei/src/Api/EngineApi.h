#pragma once

#include "Application/App.h"
#include "Startup/EngineEntryPoint.h"

inline std::shared_ptr<rei::App> App;

REI_EXTERN_API inline void CreateApplication()
{
    auto entryPoint = rei::EngineEntryPoint();
    entryPoint.ConfigureEngine();
    App = entryPoint.CreateApplication();
}

REI_EXTERN_API inline void StartApplication()
{
    App->Start();
}

REI_EXTERN_API inline int StopApplication(const int exitCode)
{
    App->Shutdown(exitCode);

    return 0;
}

typedef void (* LogCallbackDelegate) (const rei::logging::LogMessage& msg);
REI_EXTERN_API inline void AddLogCallback(const LogCallbackDelegate callback)
{
    const auto callbackPtr = std::make_shared<std::function<void(const rei::logging::LogMessage&)>>([=](const rei::logging::LogMessage& message) { callback(message); });
    rei::logging::Log::AddLogCallback(callbackPtr);
}