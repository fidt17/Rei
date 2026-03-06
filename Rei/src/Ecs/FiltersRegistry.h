#pragma once
#include "Filter.h"
#include "TypeId.h"

namespace rei::ecs
{
    template <typename... Ts>
    class TypeMask : public BitMask
    {
    public:
        TypeMask()
        {
            (Set(TypeId::Get<Ts>(), true), ...);
        }
    };

    template <typename... Ts>
    using Include = TypeMask<Ts...>;
    
    template <typename... Ts>
    using Exclude = TypeMask<Ts...>;

    class FilterProvider
    {
    public:
        template <typename... Ti>
        std::shared_ptr<Filter> Get()
        {
            return GetFilter(Include<Ti...>(), BitMask());
        }

        template <typename... Ti>
        std::shared_ptr<Filter> Get(const BitMask excludeMask)
        {
            return GetFilter(Include<Ti...>(), excludeMask);
        }

        REI_API virtual std::shared_ptr<Filter> GetFilter(const BitMask& includeMask, const BitMask& excludeMask) = 0;
    };
    
    class FiltersRegistry : public FilterProvider
    {
    public:
        eventpp::CallbackList<void()> NewFilterCreatedEvent;
        
        REI_API u32 GetFiltersCount() const;
        
        void HandleEntityChange(Entity e, const BitMask& mask) const;
        void ResizeMasks(size_t size) const;

        REI_API std::shared_ptr<Filter> GetFilter(const BitMask& includeMask, const BitMask& excludeMask) override;
        
    private:
        std::vector<std::shared_ptr<Filter>> _filters;
    };
}
