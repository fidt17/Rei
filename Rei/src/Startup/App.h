#pragma once

#include <memory>
#include <vector>

#include "Modules/Render/CustomRenderModule.h"

namespace rei
{
    class REI_API App
    {
    public:
        App() = default;
        virtual ~App() = default;
        
        virtual void OnStart() { }
        virtual void OnUpdate() { }
        virtual void OnShutdown() { }

        virtual std::vector<std::unique_ptr<render::CustomRenderModule>> CreateCustomRenderModules() { return {}; }
    };
}
