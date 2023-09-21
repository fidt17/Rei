#pragma once
#include "Entity.h"
#include "IComponentSet.h"

namespace rei::ecs
{
    template <typename T>
    class ComponentSet : public IComponentSet
    {
    public:
        explicit ComponentSet(const u64 id) : _id(id) { }

        u64 Id() const override
        {
            return _id;
        }

        T& Get(const Entity e, bool& didCreate)
        {
            if (Has(e))
            {
                didCreate = false;
                return _values.at(_indexes.at(e.Id));
            }

            Resize(e);
            _indexes.at(e.Id) = _values.size();
            _values.emplace_back(T());
            didCreate = true;

            return _values.back();
        }

        bool Has(const Entity e) const override
        {
            return _indexes.size() > e.Id && _indexes.at(e.Id) != MISSING;
        }

        bool Delete(const Entity e) override
        {
            if (!Has(e)) return false;

            _values[_indexes[e.Id]] = _values[_indexes.back()];
            _indexes.back() = _indexes[e.Id];
            _indexes[e.Id] = MISSING;

            return true;
        }

    private:
        const u64 _id;
        std::vector<i32> _indexes{};
        std::vector<T> _values{};
        const i32 MISSING = -1;

        void Resize(const Entity e)
        {
            if (_indexes.size() > e.Id) return;
            _indexes.resize(e.Id + 1, MISSING);
        }
    };
}
