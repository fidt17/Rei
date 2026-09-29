#include "pch.h"
#include "catch_amalgamated.hpp"
#include "Common/Time/TimeService.h"
#include <thread>

using rei::time::TimeService;

TEST_CASE("Frame time starts at zero and first delta ignores startup delay", "[time]")
{
    TimeService time;
    REQUIRE(time.GetDeltaSeconds() == 0);
    REQUIRE(time.GetElapsedSeconds() == 0);
    std::this_thread::sleep_for(std::chrono::milliseconds(2));
    time.BeginFrame();
    REQUIRE(time.GetDeltaSeconds() == 0);
    REQUIRE(time.GetElapsedSeconds() > 0);
}

TEST_CASE("Frame time readings stay fixed until next frame", "[time]")
{
    TimeService time;
    time.BeginFrame();
    time.BeginFrame();
    const auto delta = time.GetDeltaSeconds();
    const auto elapsed = time.GetElapsedSeconds();
    std::this_thread::sleep_for(std::chrono::milliseconds(2));
    REQUIRE(time.GetDeltaSeconds() == delta);
    REQUIRE(time.GetElapsedSeconds() == elapsed);
}

TEST_CASE("Frame delta equals elapsed progress across frames", "[time]")
{
    TimeService time;
    time.BeginFrame();
    for (i32 frame = 0; frame < 3; frame++)
    {
        const auto previous = time.GetElapsedSeconds();
        std::this_thread::sleep_for(std::chrono::milliseconds(2));
        time.BeginFrame();
        REQUIRE(time.GetDeltaSeconds() > 0);
        REQUIRE(time.GetElapsedSeconds() > previous);
        REQUIRE(time.GetElapsedSeconds() - previous == Catch::Approx(time.GetDeltaSeconds()).margin(1e-9));
    }
}

TEST_CASE("Reset clears previous session and zeroes its next delta", "[time]")
{
    TimeService time;
    time.BeginFrame();
    std::this_thread::sleep_for(std::chrono::milliseconds(2));
    time.BeginFrame();
    REQUIRE(time.GetDeltaSeconds() > 0);
    time.Reset();
    REQUIRE(time.GetDeltaSeconds() == 0);
    REQUIRE(time.GetElapsedSeconds() == 0);
    std::this_thread::sleep_for(std::chrono::milliseconds(2));
    time.BeginFrame();
    REQUIRE(time.GetDeltaSeconds() == 0);
    time.BeginFrame();
    REQUIRE(time.GetDeltaSeconds() >= 0);
}

TEST_CASE("Independent frame clocks do not mutate each other", "[time]")
{
    TimeService first;
    TimeService second;
    first.BeginFrame();
    const auto elapsed = first.GetElapsedSeconds();
    second.BeginFrame();
    second.Reset();
    REQUIRE(first.GetElapsedSeconds() == elapsed);
    REQUIRE(first.GetDeltaSeconds() == 0);
}
