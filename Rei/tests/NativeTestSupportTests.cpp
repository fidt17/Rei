#include "pch.h"
#include "support/NativeTestSupport.h"

using namespace rei::tests;

TEST_CASE("HARNESS-01 Child assertions complete successfully", "[native][harness][coverage]")
{
    Isolated([] { REQUIRE(ExecutablePath().filename() == L"Rei.exe"); });
}

TEST_CASE("HARNESS-02 Child assertion failure is observable", "[native][harness][coverage]")
{
    if (IsIsolatedChild()) { FAIL("deliberate child assertion failure"); return; }
    const auto result = RunChild(Catch::getResultCapture().getCurrentTestName());
    REQUIRE_FALSE(result.TimedOut);
    REQUIRE(result.ExitCode != 0);
    REQUIRE(result.Output.find("deliberate child assertion failure") != std::string::npos);
}

TEST_CASE("HARNESS-03 Hung child is terminated within deadline", "[native][harness][coverage]")
{
    if (IsIsolatedChild()) { Sleep(INFINITE); return; }
    const auto result = RunChild(Catch::getResultCapture().getCurrentTestName(), 1000);
    REQUIRE(result.TimedOut);
    REQUIRE(result.ExitCode == ERROR_TIMEOUT);
}

TEST_CASE("HARNESS-04 Owned temporary data is removed during exception unwinding", "[native][harness][coverage]")
{
    std::filesystem::path file;
    try
    {
        TemporaryDirectory directory;
        file = directory.Write("owned.bin", {1, 2, 3});
        REQUIRE(ReadBytes(file) == std::vector<u8>{1, 2, 3});
        throw std::runtime_error("fixture action failed");
    }
    catch (const std::runtime_error& error)
    {
        REQUIRE(std::string(error.what()) == "fixture action failed");
    }
    REQUIRE_FALSE(file.empty());
    CHECK_FALSE(std::filesystem::exists(file.parent_path()));
}
