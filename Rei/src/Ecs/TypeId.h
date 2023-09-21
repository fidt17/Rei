#pragma once

namespace rei::ecs
{
    class TypeId
    {
    public:
        template <typename>
        static u64 Get()
        {
            static const u32 ID = Allocate();
            return ID;
        }

    private:
        static u64 Allocate()
        {
            static u32 id = 0;
            return id++;
        }
    };
}
