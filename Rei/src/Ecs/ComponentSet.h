#pragma once
#include "Ecs.h"
#include "../Common/Logging/Log.h"

namespace rei::ecs
{
    class ISet
    {
    public:
        virtual ~ISet() = default;
        
        virtual bool Has(const Entity e) const = 0;
    };

    template <typename T>
    class ComponentSet : public ISet
    {
    public:
        T& Get(const Entity e)
        {
            if (this->Has(e)) return _values.at(_indexes.at(e.Id));

            Resize(e);
            _indexes.at(e.Id) = _values.size();
            _values.emplace_back(T());
            return _values.back();
        }

        bool Has(const Entity e) const override
        {
            return _indexes.size() > e.Id && _indexes.at(e.Id) != MISSING;
        }

        void Delete(const Entity e)
        {
            if (!this->Has(e)) return;

            _values[_indexes[e.Id]] = _values[_indexes.back()];
            _indexes.back() = _indexes[e.Id];
            _indexes[e.Id] = MISSING;
        }

        void PrintSet() const
        {
            std::string message = "Idx: ";
            for (const auto idx : _indexes)
            {
                message += std::to_string(idx) + " ";
            }
            LOG(message)
        }

    private:
        std::vector<i32> _indexes;
        std::vector<T> _values;
        const i32 MISSING = -1;
        
        void Resize(const Entity e)
        {
            if (_indexes.size() > e.Id) return;
            _indexes.resize(e.Id + 1, MISSING);
        }
    };
}
