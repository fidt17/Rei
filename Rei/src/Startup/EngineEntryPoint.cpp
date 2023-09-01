#include "EngineEntryPoint.h"

namespace rei
{
    SET_LOG_SCOPE("ENGINE ENTRY POINT")
    
    void EngineEntryPoint::ConfigureFramework()
    {
        logging::Log::Initialize();
        
        LOG("Configure Framework")
        
        _scope.Configure();
    }

    std::shared_ptr<Engine> EngineEntryPoint::CreateEngine() const
    {
        LOG("Create Engine")
        
        auto engine = _scope.GetEngineFactory()->CreateShared();
        engine->Configure();
        
        return engine;
    }
}
