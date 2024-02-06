#pragma once
#include "Modules/Components/EntityInfo.h"
#include "Modules/EntityManagement/EntityManager.h"

class HelloBehaviour : public rei::Behaviour
{
private:
    BEHAVIOUR_BODY(HelloBehaviour)

    SERIALIZED std::string _property;
    SERIALIZED std::string _property2;
    SERIALIZED std::string _property3;

    std::string _name;
    int _deathCounter = 3;
    
public:
    void Init() override
    {
        ECS_WORLD(rei::GetInternalWorld());
        _name = GET(GetEntity(), EntityInfo).Name;
        
        LOG("INIT: " + _name);
    }

    void Start() override
    {
        LOG("START: " + _name);
    }

    void Update() override
    {
        LOG("UPDATE: " + _name);

        if (_deathCounter-- <= 0)
        {
            rei::GetEntityManager().DestroyEntity(GetEntity());
        }
    }

    void Dispose() override
    {
        LOG("DISPOSE: " + _name);
    }
};
