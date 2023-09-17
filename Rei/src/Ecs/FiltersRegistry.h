#pragma once
#include "Filter.h"

namespace rei::ecs
{
    class FiltersRegistry
    {
    public:
        std::shared_ptr<Filter> CreateFilter()
        {
            _filters.push_back(std::make_shared<Filter>());
            return _filters.back();;
        }

        const std::vector<std::shared_ptr<Filter>>& GetFilters() const { return _filters; }

    private:
        std::vector<std::shared_ptr<Filter>> _filters;
    };
}
