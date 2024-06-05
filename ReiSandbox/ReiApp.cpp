#include "Startup/AppEntryPoint.h"

class ProjectApplication final : public rei::App
{
public:
    void OnStart() override
    {
    }

    void OnUpdate() override
    {
    }

    void OnShutdown() override
    {
    }
};

std::shared_ptr<rei::App> CreateApp()
{
    return std::make_shared<ProjectApplication>();
}
