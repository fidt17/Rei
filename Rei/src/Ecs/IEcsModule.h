#pragma once

namespace rei::ecs
{
    class IEcsModule
    {
    public:
        virtual ~IEcsModule() = default;
        virtual void AddSystems(std::shared_ptr<World>) = 0;
    };
}
