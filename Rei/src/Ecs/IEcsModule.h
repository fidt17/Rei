#pragma once

namespace rei::ecs
{
    class IEcsModule
    {
    public:
        virtual ~IEcsModule() = default;
        virtual void Configure(std::shared_ptr<World>) = 0;
    };
}
