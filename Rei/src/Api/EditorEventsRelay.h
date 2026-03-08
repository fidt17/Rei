#pragma once
#include "EditorInputEvent.h"
#include "Modules/Render/Modules/GridRenderModule.h"

namespace rei::api
{
    class EditorEventsRelay
    {
    public:
        REI_EVENT(const render::GridRenderSettings&) GridRenderSettingsReceivedEvent;
        REI_EVENT(const EditorInputEvent&) EditorInputReceivedEvent;
    };
}
