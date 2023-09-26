#pragma once

#ifdef REI_EDITOR

    typedef void (* LogCallbackDelegate) (const rei::common::logging::LogMessage& msg);
    REI_EXTERN_API inline void AddLogCallback(const LogCallbackDelegate callback)
    {
        const auto callbackPtr = std::make_shared<std::function<void(const rei::common::logging::LogMessage&)>>([=](const rei::common::logging::LogMessage& message) { callback(message); });
        rei::common::logging::Log::AddLogCallback(callbackPtr);
    }

#endif
