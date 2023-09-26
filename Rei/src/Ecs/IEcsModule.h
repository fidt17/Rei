#pragma once

namespace rei::ecs
{
    class IEcsModule
    {
    public:
        virtual void Configure(std::shared_ptr<World>) = 0;
    };
}
