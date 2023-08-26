#pragma once

#include "App.h"
#include "Common/IFactory.h"

namespace rei
{
    class AppFactory : public IFactory<App>
    {
    public:
        App CreateInstance() const override;
    };
}
