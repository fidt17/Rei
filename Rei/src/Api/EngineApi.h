#pragma once

#include "Startup/EngineEntryPoint.h"

#ifdef REI_APP

inline std::shared_ptr<rei::Engine> Engine;

REI_EXTERN_API inline void CreateApplication()
{
    auto entryPoint = rei::EngineEntryPoint();
    entryPoint.ConfigureFramework();
    Engine = entryPoint.CreateEngine();
}

REI_EXTERN_API inline void StartApplication()
{
    Engine->Start();
    OnProjectStart();
}

REI_EXTERN_API inline int StopApplication(const int exitCode)
{
    Engine->Shutdown(exitCode);
    OnProjectShutdown();

    return 0;
}

typedef void (* LogCallbackDelegate) (const rei::logging::LogMessage& msg);
REI_EXTERN_API inline void AddLogCallback(const LogCallbackDelegate callback)
{
    const auto callbackPtr = std::make_shared<std::function<void(const rei::logging::LogMessage&)>>([=](const rei::logging::LogMessage& message) { callback(message); });
    rei::logging::Log::AddLogCallback(callbackPtr);
}

#endif