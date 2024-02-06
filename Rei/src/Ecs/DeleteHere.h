#pragma once

template <typename T>
class DeleteHere final : public rei::ecs::System
{
private:
    std::shared_ptr<rei::ecs::Filter> _f;
    
public:
    DeleteHere(const std::shared_ptr<rei::ecs::EcsRegistry>& ecs, const std::shared_ptr<rei::ecs::FilterProvider>& filters)
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
};
