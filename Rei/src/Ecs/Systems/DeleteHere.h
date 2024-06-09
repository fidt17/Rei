#pragma once
#include "Ecs/System.h"

namespace rei::ecs
{
    template <typename T>
    class DeleteHere final : public System
    {
    public:
        DeleteHere(const std::shared_ptr<EcsRegistry>& ecs, const std::shared_ptr<FilterProvider>& filters)
            : System(ecs, filters)
        {
            _f = filters->Get<T>();
        }

        void OnUpdate() override
        {
            FOR(e, _f)
            {
                DEL(e, T);
            }
        }

    private:
        std::shared_ptr<Filter> _f;
    };
}
