#pragma once

namespace rei::ecs
{
    class TypeId
    {
    public:
        template <typename>
        static u32 Get()
        {
            static const u32 ID = Allocate();
            return ID;
        }

    private:
        static u32 Allocate()
        {
            static u32 id = 0;
            return id++;
        }
    };
}
