#include "EngineEntryPoint.h"

namespace rei
{
    SET_LOG_SCOPE("ENGINE ENTRY POINT")
    
    void EngineEntryPoint::ConfigureEngine()
    {
        logging::Log::Initialize();
        
        LOG("Configure Engine")
        
        _scope.Configure();
    }

    std::shared_ptr<App> EngineEntryPoint::CreateApplication() const
    {
        LOG("Create Application")
        
        auto app = _scope.GetAppFactory()->CreateShared();
        app->Configure();
        
        return app;
    }
}

int main()
{
    auto entryPoint = rei::EngineEntryPoint();
    
    entryPoint.ConfigureEngine();
    const auto app = entryPoint.CreateApplication();
    app->Start();

    std::cin.get();
    app->Shutdown(1);

    return 0;
}
