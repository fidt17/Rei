#pragma once

namespace rei::ecs
{
    class TypeId
    {
    public:
        template <typename>
        static size_t Get()
        {
            static const size_t ID = Allocate();
            return ID;
        }

    private:
        static size_t Allocate()
        {
            static u32 id = 0;
            return id++;
        }
    };
}
