#include "pch.h"
#include "catch_amalgamated.hpp"

class TestInitialization : public Catch::EventListenerBase
{
public:
    using EventListenerBase::EventListenerBase;

    void testRunStarting(Catch::TestRunInfo const&) override
    {
        rei::logging::Log::Initialize();
    }
};

CATCH_REGISTER_LISTENER(TestInitialization)
