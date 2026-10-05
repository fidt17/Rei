#include "pch.h"
#include "support/NativeEngineFixture.h"
#include <future>

using namespace rei;
using namespace rei::tests;
using namespace rei::internal::engine;

namespace
{
    void CheckCancelledStartup(const bool delayCreation)
    {
        std::promise<void> entered, resumed;
        auto enteredFuture = entered.get_future();
        const auto resume = resumed.get_future().share();
        NativeEngineFixture fixture;
        const auto delayed = [&] { entered.set_value(); resume.wait(); };
        if (delayCreation) fixture.App->CreateModulesAction = delayed;
        else fixture.BeforeStartAction = delayed;
        std::jthread controller([&]
        {
            if (enteredFuture.wait_for(std::chrono::seconds(2)) != std::future_status::ready) return;
            std::this_thread::sleep_for(std::chrono::milliseconds(200));
            resumed.set_value();
        });
        CHECK_THROWS_WITH(fixture.Start(100), "Native engine readiness timeout");
        const auto stopAt = std::chrono::steady_clock::now();
        CHECK_NOTHROW(fixture.Stop());
        controller.join();
        CHECK(std::chrono::steady_clock::now() - stopAt < std::chrono::seconds(2));
        REQUIRE(fixture.Engine != nullptr);
        CHECK_FALSE(fixture.Engine->IsRunning());
        CHECK(fixture.App->Starts == 0);
        CHECK(fixture.App->Updates == 0);
    }
}

TEST_CASE("HARNESS-10 Startup timeout cancels late engine publication before joining", "[native][harness][engine-integration][coverage][coverage-remaining][isolated]")
{
    Isolated([] { CheckCancelledStartup(true); }, 10000, 1024);
}

TEST_CASE("HARNESS-11 Startup timeout cancels published engine before Start enters loop", "[native][harness][engine-integration][coverage][coverage-remaining][isolated]")
{
    Isolated([] { CheckCancelledStartup(false); }, 10000, 1024);
}

TEST_CASE("HARNESS-06 Expired queued engine action stays cancelled after drain resumes", "[native][harness][engine-integration][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        auto entered = std::make_shared<std::promise<void>>();
        auto resumed = std::make_shared<std::promise<void>>();
        auto enteredFuture = entered->get_future();
        const auto resume = resumed->get_future().share();
        std::atomic<bool> block = true;
        NativeEngineFixture fixture;
        fixture.App->UpdateAction = [&, entered, resume]
        {
            if (block.exchange(false)) { entered->set_value(); resume.wait(); }
        };
        fixture.Start();
        REQUIRE(enteredFuture.wait_for(std::chrono::seconds(2)) == std::future_status::ready);
        std::atomic<i32> invoked = 0;
        CHECK_THROWS_WITH(fixture.OnEngineThread([&] { ++invoked; }, 20), "Native fixture action timeout");
        resumed->set_value();
        fixture.OnEngineThread([] {});
        CHECK(invoked == 0);
        fixture.Stop();
    }, 20000, 1024);
}

TEST_CASE("HARNESS-07 Running engine action keeps caller captures alive beyond request deadline", "[native][harness][engine-integration][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        std::promise<void> entered, resumed;
        auto enteredFuture = entered.get_future();
        const auto resume = resumed.get_future().share();
        NativeEngineFixture fixture;
        fixture.Start();
        fixture.WaitForNextFrames(2);
        std::jthread controller([&]
        {
            if (enteredFuture.wait_for(std::chrono::seconds(10)) != std::future_status::ready) return;
            std::this_thread::sleep_for(std::chrono::milliseconds(5200));
            resumed.set_value();
        });
        i32 result = 0;
        fixture.OnEngineThread([&] { entered.set_value(); resume.wait(); result = 91; }, 5000);
        controller.join();
        CHECK(result == 91);
        fixture.Stop();
    }, 25000, 1024);
}

TEST_CASE("HARNESS-08 Engine action exception reaches caller and does not end worker", "[native][harness][engine-integration][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture fixture;
        fixture.Start();
        CHECK_THROWS_WITH(fixture.OnEngineThread([] { throw std::runtime_error("owned action failure"); }), "owned action failure");
        bool nextAction = false;
        fixture.OnEngineThread([&] { nextAction = true; });
        CHECK(nextAction);
        CHECK(fixture.Engine->IsRunning());
        fixture.Stop();
    }, 20000, 1024);
}

TEST_CASE("HARNESS-09 Explicit Stop propagates worker failure after readiness", "[native][harness][engine-integration][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        std::atomic<bool> armed = false, shutdownHook = false;
        NativeEngineFixture fixture;
        fixture.App->StartAction = [&]
        {
            GetEngine().ShutdownEvent.append([&](i32)
            {
                shutdownHook = true;
                throw std::runtime_error("worker shutdown notification failure");
            });
        };
        fixture.App->UpdateAction = [&] { if (armed) throw std::runtime_error("trigger worker shutdown"); };
        fixture.Start();
        fixture.OnEngineThread([&] { armed = true; });
        const auto deadline = std::chrono::steady_clock::now() + std::chrono::seconds(2);
        while (!shutdownHook && std::chrono::steady_clock::now() < deadline) std::this_thread::yield();
        REQUIRE(shutdownHook);
        CHECK_THROWS_WITH(fixture.Stop(), "worker shutdown notification failure");
        CHECK_FALSE(fixture.Engine->IsRunning());
        CHECK(GetAssetManager().GetLoadedAssetCount() == 0);
    }, 20000, 1024);
}

TEST_CASE("BEH-05 Play control starts and updates live component across completed native frames", "[native][engine-integration][behaviour][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        NativeEngineFixture fixture;
        fixture.Start();
        ecs::Entity entity = ecs::NULL_ENTITY;
        fixture.OnEngineThread([&]
        {
            entity = GetEntityManager().CreateNewEntity("frame control");
            GetEntityManager().AddBehaviour(entity, PROBE_A, nlohmann::json());
        });
        const auto completed = fixture.WaitForNextFrames(2);
        i32 starts = 0, updates = 0;
        fixture.OnEngineThread([&]
        {
            starts = fixture.Resources.Count(PROBE_A, "Start");
            updates = fixture.Resources.Count(PROBE_A, "Update");
            CHECK(GetInternalWorld()->GetRegistry()->IsAlive(entity));
        });
        CHECK(completed >= 2);
        CHECK(starts == 1);
        CHECK(updates >= 2);
        fixture.Stop();
    }, 20000, 1024);
}
