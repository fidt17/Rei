#pragma once
#include "Modules/Resources/AssetBuilder.h"

typedef void (* LogCallbackDelegate)(const rei::common::logging::LogMessage& msg);
REI_EXTERN_API inline void AddLogCallback(const LogCallbackDelegate callback)
{
    const auto callbackPtr = std::make_shared<std::function<void(const rei::common::logging::LogMessage&)>>([=](const rei::common::logging::LogMessage& message)
    {
        callback(message);
    });
    rei::common::logging::Log::AddLogCallback(callbackPtr);
}

REI_EXTERN_API inline i64 BuildAsset(const char* file, const char* dest, const i64 offset)
{
    return rei::resources::BuildAsset(file, dest, offset);
}
