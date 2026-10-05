#include "pch.h"
#include "support/NativeTestSupport.h"

using namespace rei::tests;

TEST_CASE("HARNESS-05 Parent cleans child temporary resources after abrupt process exit", "[native][harness][coverage][coverage-remaining]")
{
    if (IsIsolatedChild())
    {
        TemporaryDirectory directory;
        directory.Write("abandoned.bin", {1, 2, 3});
        std::_Exit(77);
    }
    const auto child = RunChild(Catch::getResultCapture().getCurrentTestName());
    CHECK_FALSE(child.TimedOut);
    CHECK(child.ExitCode == 77);
    CHECK_FALSE(std::filesystem::exists(child.Report.parent_path() / "temporary"));
    CHECK(std::filesystem::exists(child.Report.parent_path()));
}

TEST_CASE("HARNESS-12 Deadline terminates child holding exclusive temporary file before cleanup", "[native][harness][coverage][coverage-remaining]")
{
    if (IsIsolatedChild())
    {
        TemporaryDirectory directory;
        const auto file = directory.File("held-open.bin");
        WinHandle held(CreateFileW(file.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr));
        Sleep(INFINITE);
        return;
    }
    const auto child = RunChild(Catch::getResultCapture().getCurrentTestName(), 2000);
    CHECK(child.TimedOut);
    CHECK(child.ExitCode == ERROR_TIMEOUT);
    CHECK_FALSE(std::filesystem::exists(child.Report.parent_path() / "temporary"));
    const auto identity = nlohmann::json::parse(ReadText(child.Report.parent_path() / "process.json"));
    CHECK(identity.at("ProcessId").get<u32>() != GetCurrentProcessId());
    CHECK(identity.at("CreationFileTime").get<u64>() > 0);
}
