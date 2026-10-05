#pragma once
#include "Common/Primitives.h"

namespace rei::tests
{
    // Test-only ABI: journal memory and callback implementation belong to host.
    enum class ProjectJournalEvent : i32
    {
        AssetCreated, AssetDestroyed, BehaviourCreated, BehaviourDisposed,
        AppCreated, AppDestroyed, CallbackCreated, CallbackDestroyed, AppShutdown, Ready,
        CreationEntered, StartEntered, WorkerExceptionDestroyed, Count
    };
    using ProjectJournalCallback = void (*)(void*, i32);
    using ProjectCancelCallback = bool (*)(void*);
}
