#pragma once

// https://skypjack.github.io/2020-03-14-ecs-baf-part-8/
namespace rei::ecs
{
#ifdef REI_ENGINE
#define TYPE_ID_GENERATOR_API __declspec(dllexport)
#else
#define TYPE_ID_GENERATOR_API __declspec(dllimport)
#endif

    namespace typeId::internal
    {
        struct TYPE_ID_GENERATOR_API generator
        {
            static std::size_t next()
            {
                static std::size_t value{0};
                return value++;
            }
        };

        template <typename Type>
        struct TYPE_ID_GENERATOR_API type
        {
            static std::size_t id()
            {
                static const std::size_t value = generator::next();
                return value;
            }
        };
    }

    class TypeId
    {
    public:
        template <typename T>
        static size_t Get()
        {
            return typeId::internal::type<T>().id();
        }
    };
}
