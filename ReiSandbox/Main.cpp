using namespace rei::ecs;

struct LogMessage
{
    std::string Message;
};

class HelloWorldSystem final : public System
{
public:
    HelloWorldSystem(const std::shared_ptr<EcsRegistry>& ecs, const std::shared_ptr<FilterProvider>& filters)
        : System(ecs, filters)
    {
    }

    void OnUpdate() override
    {
        FOR(_f)
        {
            LOG(GET(e, LogMessage).Message);
        }
    }

private:
    std::shared_ptr<Filter> _f;
};

class ProjectApplication final : public rei::App
{
public:
    std::shared_ptr<World> _world;
    
    void OnStart() override
    {
        LOG("APP START")
        
        _world = std::make_shared<World>();
        _world->AddSystem<HelloWorldSystem>();

        ECS_WORLD(*_world);
        auto e = NEW_ENTITY();
        GET(e, LogMessage) = { "hello there" };
    }

    void OnUpdate() override
    {
        LOG("APP UPDATE")
        _world->Run();
    }

    void OnShutdown() override
    {
        LOG("APP SHUTDOWN")
    }
};

std::shared_ptr<rei::App> CreateApp() { return std::make_shared<ProjectApplication>(); }
