#pragma once
#include "Modules/Render/Modules/GridRenderModule.h"

namespace rei::api
{
    class EditorEventsRelay
    {
    public:
        REI_EVENT(const render::GridRenderSettings&) GridRenderSettingsReceivedEvent;
    };
}
