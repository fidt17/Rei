#include "EngineEntryPoint.h"

namespace rei
{
    void EngineEntryPoint::ConfigureEngine()
    {
        Log::Initialize();
        
        LOG("[EngineEntryPoint] Configure Engine")
        
        _scope.Configure();
    }

    std::shared_ptr<App> EngineEntryPoint::CreateApplication() const
    {
        LOG("[EngineEntryPoint] Create Application")
        
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
