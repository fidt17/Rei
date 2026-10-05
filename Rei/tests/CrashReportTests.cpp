#include "pch.h"
#include "support/NativeTestSupport.h"
#include "Common/Diagnostics/CrashReporter.h"
#include <cstdlib>
#include <crtdbg.h>

using namespace rei::tests;
using rei::common::diagnostics::CrashReporter;

namespace
{
    class CrashDirectoryOverride
    {
    public:
        explicit CrashDirectoryOverride(const std::filesystem::path& path)
        {
            char* previous = nullptr;
            size_t length = 0;
            _dupenv_s(&previous, &length, "REI_CRASH_TEST_DIR");
            if (previous) { _previous = previous; std::free(previous); }
            _putenv_s("REI_CRASH_TEST_DIR", path.string().c_str());
        }
        ~CrashDirectoryOverride() { _putenv_s("REI_CRASH_TEST_DIR", _previous.c_str()); }
    private:
        std::string _previous;
    };

    std::string CrashReports(const TemporaryDirectory& directory)
    {
        std::string result;
        const auto path = directory.File("crash_reports");
        if (!std::filesystem::exists(path)) return result;
        for (const auto& entry : std::filesystem::directory_iterator(path)) result += ReadText(entry.path());
        return result;
    }

    void VerifyFatalReport(const std::function<void()>& crash, const std::string& event)
    {
        if (IsIsolatedChild())
        {
            SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX);
            _set_abort_behavior(0, _WRITE_ABORT_MSG | _CALL_REPORTFAULT);
            _CrtSetReportMode(_CRT_ASSERT, _CRTDBG_MODE_FILE);
            _CrtSetReportFile(_CRT_ASSERT, _CRTDBG_FILE_STDERR);
            _CrtSetReportMode(_CRT_ERROR, _CRTDBG_MODE_FILE);
            _CrtSetReportFile(_CRT_ERROR, _CRTDBG_FILE_STDERR);
            const auto directory = std::getenv("REI_CRASH_TEST_DIR");
            if (!directory) std::_Exit(90);
            CrashReporter::Initialize(directory, "NativeCrashProbe");
            crash();
            std::_Exit(91);
        }
        TemporaryDirectory directory;
        CrashDirectoryOverride environment(directory.File(""));
        const auto child = RunChild(Catch::getResultCapture().getCurrentTestName());
        INFO("Child report: " << child.Report.string());
        REQUIRE_FALSE(child.TimedOut);
        REQUIRE(child.ExitCode != 0);
        REQUIRE(child.ExitCode != 90);
        REQUIRE(child.ExitCode != 91);
        const auto reports = CrashReports(directory);
        INFO(reports);
        CHECK(reports.find("REI CRASH REPORT") != std::string::npos);
        CHECK(reports.find("Application: NativeCrashProbe") != std::string::npos);
        CHECK(reports.find("Process Id:") != std::string::npos);
        CHECK(reports.find(event) != std::string::npos);
    }
}

TEST_CASE("CRASH-01 Abort handler writes report in owned subprocess directory", "[native][diagnostics][coverage][coverage-remaining][isolated]")
{
    VerifyFatalReport([] { std::raise(SIGABRT); }, "Event: Signal");
}

TEST_CASE("CRASH-01 Terminate handler retains exception reason", "[native][diagnostics][coverage][coverage-remaining][isolated]")
{
    VerifyFatalReport([]
    {
        std::thread crash([] { throw std::runtime_error("native terminate probe"); });
        crash.join();
    }, "native terminate probe");
}

TEST_CASE("CRASH-01 Explicit crash report contains recent diagnostic logs", "[native][diagnostics][coverage][coverage-remaining][isolated]")
{
    Isolated([]
    {
        TemporaryDirectory directory;
        CrashReporter::Initialize(directory.File(""), "NativeCrashProbe");
        LOG_ERROR("owned native diagnostic marker")
        CrashReporter::WriteCrash("test report", "specific failure details");
        const auto report = CrashReports(directory);
        CHECK(report.find("Event: test report") != std::string::npos);
        CHECK(report.find("specific failure details") != std::string::npos);
        CHECK(report.find("owned native diagnostic marker") != std::string::npos);
    });
}
