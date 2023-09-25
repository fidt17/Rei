#pragma once
#include "Engine/Engine.h"

namespace rei::internal::entry_point
{
    class EngineEntryPoint
    {
    public:
        REI_API void Initialize()
        {
            logging::Log::Initialize();
        }

        REI_API std::shared_ptr<engine::Engine> CreateEngine(std::shared_ptr<App> app) const
        {
            auto engine = std::make_shared<engine::Engine>(app);
            return engine;
        }
    };
}
