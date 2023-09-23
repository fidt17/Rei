#pragma once
#include "Entity.h"
#include "IComponentSet.h"

namespace rei::ecs
{
    template <typename T>
    class ComponentSet : public IComponentSet
    {
    public:
        explicit ComponentSet(const size_t id) : _id(id) { }

        size_t Id() const override
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
            _indexes.at(e.Id) = static_cast<EntityId>(_values.size());
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
        const size_t _id;
        std::vector<EntityId> _indexes{};
        std::vector<T> _values{};
        const EntityId MISSING = -1;

        void Resize(const Entity e)
        {
            if (_indexes.size() > e.Id) return;
            _indexes.resize(e.Id + 1, MISSING);
        }
    };
}
