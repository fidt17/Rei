#pragma once
#include "../Core.h"

namespace rei
{
    class REI_EXPORT App
    {
    public:
        void Start();

        int GetAppNumber() const;

    private:
        int _appNumber = 0;
    };
}
