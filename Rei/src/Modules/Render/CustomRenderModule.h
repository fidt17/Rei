#pragma once

#include "RenderContext.h"

namespace rei::render
{
    class REI_API CustomRenderModule
    {
    public:
        virtual ~CustomRenderModule() = default;

        virtual void Setup() { }
        virtual void Render(const RenderContext& context) = 0;
        virtual void Dispose() { }
    };
}
