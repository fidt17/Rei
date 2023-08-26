#pragma once
#include <memory>

template <typename T>
class IFactory
{
public:
    virtual T CreateInstance() const = 0;
    virtual std::shared_ptr<T> CreateShared() const { return std::make_shared<T>(CreateInstance()); }
    virtual std::unique_ptr<T> CreateUnique() const { return std::make_unique<T>(CreateInstance()); }
};
