#include "pch.h"
#include "Services.h"

namespace rei
{
    Services* Services::_instance = nullptr;

    Services* Services::GetInstance()
    {
        if (_instance == nullptr)
        {
            _instance = new Services();
        }
        return _instance;
    }
}
