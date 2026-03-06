#pragma once

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
    };
}
