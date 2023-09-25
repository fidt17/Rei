#pragma once

// todo: REI_EDITOR
#ifdef REI_APP

typedef void (* LogCallbackDelegate) (const rei::logging::LogMessage& msg);
REI_EXTERN_API inline void AddLogCallback(const LogCallbackDelegate callback)
{
    const auto callbackPtr = std::make_shared<std::function<void(const rei::logging::LogMessage&)>>([=](const rei::logging::LogMessage& message) { callback(message); });
    rei::logging::Log::AddLogCallback(callbackPtr);
}

#endif
