#pragma once

#include "catch_amalgamated.hpp"
#include <atomic>
#include <filesystem>
#include <fstream>
#include <functional>
#include <iterator>
#include <memory>
#include <vector>

#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>

namespace rei::tests
{
    inline std::filesystem::path CreateOwnedDirectory(const std::filesystem::path& parent)
    {
        static std::atomic<u64> nextId = 0;
        std::filesystem::create_directories(parent);
        for (u32 attempt = 0; attempt < 100; ++attempt)
        {
            const auto name = std::to_string(GetCurrentProcessId()) + "-" + std::to_string(GetTickCount64()) + "-" + std::to_string(nextId++);
            const auto path = parent / name;
            if (std::filesystem::create_directory(path)) return path;
        }
        throw std::runtime_error("Could not create an owned native test directory");
    }

    class TemporaryDirectory
    {
    public:
        TemporaryDirectory() : _path(CreateOwnedDirectory(TemporaryRoot())) {}
        TemporaryDirectory(const TemporaryDirectory&) = delete;
        TemporaryDirectory& operator=(const TemporaryDirectory&) = delete;
        ~TemporaryDirectory()
        {
            std::error_code error;
            std::filesystem::remove_all(_path, error);
        }

        std::filesystem::path File(const std::string& name) const { return _path / name; }

        std::filesystem::path Write(const std::string& name, const std::vector<u8>& bytes) const
        {
            const auto path = File(name);
            std::ofstream stream(path, std::ios::binary);
            stream.write(reinterpret_cast<const char*>(bytes.data()), static_cast<std::streamsize>(bytes.size()));
            stream.close();
            if (!stream) throw std::runtime_error("Could not write native test data");
            return path;
        }

    private:
        std::filesystem::path _path;

        static std::filesystem::path TemporaryRoot()
        {
            std::wstring value(32768, L'\0');
            const auto length = GetEnvironmentVariableW(L"REI_NATIVE_TEST_TEMP_ROOT", value.data(), static_cast<DWORD>(value.size()));
            if (length > 0 && length < value.size()) { value.resize(length); return value; }
            return std::filesystem::temp_directory_path() / "rei-native-tests";
        }
    };

    inline std::string ReadText(const std::filesystem::path& path)
    {
        std::ifstream stream(path, std::ios::binary);
        return {std::istreambuf_iterator<char>(stream), std::istreambuf_iterator<char>()};
    }

    inline std::vector<u8> ReadBytes(const std::filesystem::path& path)
    {
        const auto text = ReadText(path);
        return {text.begin(), text.end()};
    }

    class WinHandle
    {
    public:
        explicit WinHandle(HANDLE value) : _value(value)
        {
            if (value == nullptr || value == INVALID_HANDLE_VALUE) throw std::runtime_error("Native test handle failed: " + std::to_string(GetLastError()));
        }
        WinHandle(const WinHandle&) = delete;
        WinHandle& operator=(const WinHandle&) = delete;
        ~WinHandle() { CloseHandle(_value); }
        HANDLE Get() const { return _value; }

    private:
        HANDLE _value;
    };

    inline std::filesystem::path ExecutablePath()
    {
        std::wstring path(32768, L'\0');
        const auto count = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
        if (count == 0 || count >= path.size()) throw std::runtime_error("Could not resolve native test executable");
        path.resize(count);
        return path;
    }

    inline bool IsIsolatedChild()
    {
        wchar_t value[2]{};
        return GetEnvironmentVariableW(L"REI_NATIVE_TEST_CHILD", value, 2) == 1 && value[0] == L'1';
    }

    inline std::wstring QuoteArgument(const std::wstring& value)
    {
        std::wstring quoted = L"\"";
        u32 slashes = 0;
        for (const auto ch : value)
        {
            if (ch == L'\\') { ++slashes; continue; }
            quoted.append(ch == L'\"' ? slashes * 2 + 1 : slashes, L'\\');
            slashes = 0;
            quoted += ch;
        }
        quoted.append(slashes * 2, L'\\');
        return quoted + L'\"';
    }

    struct ChildResult
    {
        bool TimedOut = false;
        u32 ExitCode = 0;
        std::filesystem::path Report;
        std::string Output;
    };

    inline ChildResult RunChild(const std::string& caseName, const u32 timeoutMs = 5000, const u32 memoryLimitMiB = 256)
    {
        const auto executable = ExecutablePath();
        std::wstring runDirectory(32768, L'\0');
        const auto runLength = GetEnvironmentVariableW(L"REI_NATIVE_TEST_RUN_DIR", runDirectory.data(), static_cast<DWORD>(runDirectory.size()));
        std::filesystem::path reportRoot = executable.parent_path() / "test-results";
        if (runLength > 0 && runLength < runDirectory.size())
        {
            runDirectory.resize(runLength);
            reportRoot = std::filesystem::path(runDirectory) / "child-reports";
        }
        const auto report = CreateOwnedDirectory(reportRoot) / "child.txt";
        const auto temporaryRoot = report.parent_path() / "temporary";
        // Test case names are ASCII stable IDs. Paths retain their native UTF-16 encoding.
        const std::wstring wideCase(caseName.begin(), caseName.end());
        auto command = QuoteArgument(executable.wstring()) + L" " + QuoteArgument(wideCase) + L" --reporter console --colour-mode none --out " + QuoteArgument(report.wstring());
        command += L" --rng-seed " + std::to_wstring(Catch::getSeed());

        std::unique_ptr<wchar_t, decltype(&FreeEnvironmentStringsW)> currentEnvironment(GetEnvironmentStringsW(), &FreeEnvironmentStringsW);
        if (!currentEnvironment) throw std::runtime_error("Could not read native test environment");
        std::vector<wchar_t> environment;
        for (auto entry = currentEnvironment.get(); *entry != L'\0'; entry += wcslen(entry) + 1)
        {
            if (_wcsnicmp(entry, L"REI_NATIVE_TEST_CHILD=", 22) == 0) continue;
            if (_wcsnicmp(entry, L"REI_NATIVE_TEST_TEMP_ROOT=", 26) == 0) continue;
            environment.insert(environment.end(), entry, entry + wcslen(entry) + 1);
        }
        const std::wstring marker = L"REI_NATIVE_TEST_CHILD=1";
        environment.insert(environment.end(), marker.begin(), marker.end());
        environment.push_back(L'\0');
        const auto temporaryMarker = L"REI_NATIVE_TEST_TEMP_ROOT=" + temporaryRoot.wstring();
        environment.insert(environment.end(), temporaryMarker.begin(), temporaryMarker.end());
        environment.push_back(L'\0');
        environment.push_back(L'\0');

        WinHandle job(CreateJobObjectW(nullptr, nullptr));
        JOBOBJECT_EXTENDED_LIMIT_INFORMATION limits{};
        limits.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE | JOB_OBJECT_LIMIT_PROCESS_MEMORY | JOB_OBJECT_LIMIT_ACTIVE_PROCESS;
        limits.BasicLimitInformation.ActiveProcessLimit = 1;
        limits.ProcessMemoryLimit = static_cast<SIZE_T>(memoryLimitMiB) * 1024 * 1024;
        if (!SetInformationJobObject(job.Get(), JobObjectExtendedLimitInformation, &limits, sizeof(limits))) throw std::runtime_error("Could not limit native test job");

        STARTUPINFOW startup{};
        startup.cb = sizeof(startup);
        PROCESS_INFORMATION processInfo{};
        if (!CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, FALSE, CREATE_SUSPENDED | CREATE_NO_WINDOW | CREATE_UNICODE_ENVIRONMENT, environment.data(), nullptr, &startup, &processInfo))
        {
            throw std::runtime_error("Could not start native test child: " + std::to_string(GetLastError()));
        }
        WinHandle process(processInfo.hProcess);
        WinHandle thread(processInfo.hThread);
        if (!AssignProcessToJobObject(job.Get(), process.Get()))
        {
            TerminateProcess(process.Get(), ERROR_ACCESS_DENIED);
            WaitForSingleObject(process.Get(), 5000);
            throw std::runtime_error("Could not assign native test child to its owned job");
        }
        FILETIME created{}, exited{}, kernel{}, user{};
        if (!GetProcessTimes(process.Get(), &created, &exited, &kernel, &user)) throw std::runtime_error("Could not read owned child creation time");
        const auto creationTime = static_cast<u64>(created.dwLowDateTime) | (static_cast<u64>(created.dwHighDateTime) << 32);
        const auto pendingMetadata = report.parent_path() / "process.pending";
        std::ofstream metadata(pendingMetadata);
        metadata << "{\"ProcessId\":" << processInfo.dwProcessId << ",\"CreationFileTime\":" << creationTime << "}";
        metadata.close();
        if (!metadata) throw std::runtime_error("Could not record owned child process");
        // Publish identity before child can hold temporary files. Root timeout
        // cleanup waits this exact PID + creation time, never a reused PID.
        std::filesystem::rename(pendingMetadata, report.parent_path() / "process.json");
        if (ResumeThread(thread.Get()) == static_cast<DWORD>(-1)) throw std::runtime_error("Could not resume native test child");

        ChildResult result;
        result.Report = report;
        const auto wait = WaitForSingleObject(process.Get(), timeoutMs);
        result.TimedOut = wait == WAIT_TIMEOUT;
        if (wait != WAIT_OBJECT_0)
        {
            TerminateJobObject(job.Get(), ERROR_TIMEOUT);
            if (WaitForSingleObject(process.Get(), 5000) != WAIT_OBJECT_0) throw std::runtime_error("Native test child did not terminate");
            if (!result.TimedOut) throw std::runtime_error("Could not wait for native test child");
        }
        DWORD exitCode = 0;
        if (!GetExitCodeProcess(process.Get(), &exitCode)) throw std::runtime_error("Could not read native test child exit code");
        result.ExitCode = exitCode;
        result.Output = ReadText(report);
        // Root is derived from the freshly created report directory, never from
        // an inherited user path. Child is dead before recursive cleanup.
        std::error_code cleanupError;
        std::filesystem::remove_all(temporaryRoot, cleanupError);
        if (cleanupError) throw std::runtime_error("Could not clean owned child temporary data: " + cleanupError.message());
        return result;
    }

    inline void Isolated(const std::function<void()>& body, const u32 timeoutMs = 5000, const u32 memoryLimitMiB = 256)
    {
        if (IsIsolatedChild()) { body(); return; }
        const auto result = RunChild(Catch::getResultCapture().getCurrentTestName(), timeoutMs, memoryLimitMiB);
        INFO("Child report: " << result.Report.string());
        INFO(result.Output);
        REQUIRE_FALSE(result.TimedOut);
        REQUIRE(result.ExitCode == 0);
        REQUIRE(result.Output.find("All tests passed") != std::string::npos);
    }
}
