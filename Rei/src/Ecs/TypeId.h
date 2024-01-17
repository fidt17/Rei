#pragma once

#ifdef REI_ENGINE
    #define TYPE_ID_API __declspec(dllexport)
#else
    #define TYPE_ID_API __declspec(dllimport)
#endif

namespace rei::ecs
{
    namespace typeId::internal
    {
        struct TYPE_ID_API generator
        {
            static std::size_t next()
            {
                static std::size_t value{0};
                return value++;
            }
        };
    }

    class TYPE_ID_API TypeId
    {
    public:
        template <typename T>
        static size_t Get()
        {
            static const std::size_t value = typeId::internal::generator::next();
            return value;
        }
    };
}

#ifdef REI_ENGINE
#define EXPORT_COMPONENT(T)\
    template __declspec(dllexport) std::size_t rei::ecs::TypeId::Get<T>();
#else
#define EXPORT_COMPONENT(T)\
    extern template __declspec(dllimport) std::size_t rei::ecs::TypeId::Get<T>();
#endif

