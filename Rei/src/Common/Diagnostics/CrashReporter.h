#pragma once

#include <atomic>
#include <chrono>
#include <csignal>
#include <cstdlib>
#include <exception>
#include <filesystem>
#include <fstream>
#include <format>
#include <iomanip>
#include <mutex>
#include <process.h>
#include <string>
#include <thread>

#include <windows.h>

#ifdef DEBUG
#include <crtdbg.h>
#endif

#include "Common/Logging/Logger.h"

namespace rei::common::diagnostics
{
    class CrashReporter final
    {
    public:
        static void Initialize(const std::filesystem::path& basePath, const std::string& appName)
        {
            std::lock_guard lock(GetMutex());
            GetCrashDirectory() = basePath / "crash_reports";
            std::error_code error;
            std::filesystem::create_directories(GetCrashDirectory(), error);
            GetAppName() = appName;

            if (GetInitialized().exchange(true))
            {
                return;
            }

            std::set_terminate(&OnTerminate);
            std::signal(SIGABRT, &OnSignal);
            std::signal(SIGFPE, &OnSignal);
            std::signal(SIGILL, &OnSignal);
            std::signal(SIGSEGV, &OnSignal);
            SetUnhandledExceptionFilter(&OnUnhandledException);

#ifdef DEBUG
            _CrtSetReportHook2(_CRT_RPTHOOK_INSTALL, &OnCrtReport);
#endif
        }

        static void WriteCrash(const std::string& title, const std::string& details)
        {
            std::lock_guard lock(GetMutex());
            std::error_code error;
            std::filesystem::create_directories(GetCrashDirectory(), error);

            const auto now = std::chrono::system_clock::now();
            const auto time = std::chrono::system_clock::to_time_t(now);
            std::tm localTime{};
            localtime_s(&localTime, &time);

            const auto milliseconds = std::chrono::duration_cast<std::chrono::milliseconds>(now.time_since_epoch()) % 1000;
            const std::string fileName = std::format(
                "{}_crash_{:04d}{:02d}{:02d}_{:02d}{:02d}{:02d}_{:03d}_pid{}.log",
                GetAppName(),
                localTime.tm_year + 1900,
                localTime.tm_mon + 1,
                localTime.tm_mday,
                localTime.tm_hour,
                localTime.tm_min,
                localTime.tm_sec,
                static_cast<int>(milliseconds.count()),
                static_cast<int>(_getpid()));

            const auto filePath = GetCrashDirectory() / fileName;
            std::ofstream stream(filePath, std::ios::out | std::ios::trunc);
            if (!stream.is_open())
            {
                return;
            }

            stream << "REI CRASH REPORT\n";
            stream << "================\n";
            stream << "Application: " << GetAppName() << "\n";
            stream << "Timestamp: " << std::put_time(&localTime, "%Y-%m-%d %H:%M:%S") << "\n";
            stream << "Thread Id: " << std::this_thread::get_id() << "\n";
            stream << "Process Id: " << _getpid() << "\n";
            stream << "\n";
            stream << "Event: " << title << "\n";
            stream << details << "\n";
            stream << "\n";
            stream << "Recent Logs:\n";
            stream << "------------\n";

            const auto recentLogs = rei::common::logging::GetRecentLogEntriesSnapshot();
            if (recentLogs.empty())
            {
                stream << "(no log entries captured)\n";
            }
            else
            {
                for (const auto& line : recentLogs)
                {
                    stream << line << "\n";
                }
            }
        }

    private:
        static std::atomic<bool>& GetInitialized()
        {
            static std::atomic<bool> initialized = false;
            return initialized;
        }

        static std::mutex& GetMutex()
        {
            static std::mutex mutex;
            return mutex;
        }

        static std::filesystem::path& GetCrashDirectory()
        {
            static std::filesystem::path crashDirectory = std::filesystem::current_path() / "crash_reports";
            return crashDirectory;
        }

        static std::string& GetAppName()
        {
            static std::string appName = "Rei App";
            return appName;
        }

        static LONG WINAPI OnUnhandledException(EXCEPTION_POINTERS* exceptionInfo)
        {
            const auto code = exceptionInfo != nullptr && exceptionInfo->ExceptionRecord != nullptr
                ? exceptionInfo->ExceptionRecord->ExceptionCode
                : 0;
            WriteCrash("Unhandled SEH exception", std::format("Exception code: 0x{:08X}", static_cast<unsigned int>(code)));
            return EXCEPTION_EXECUTE_HANDLER;
        }

        static void OnSignal(const int signalCode)
        {
            WriteCrash("Signal", std::format("Signal code: {}", signalCode));
            std::_Exit(EXIT_FAILURE);
        }

        static void OnTerminate()
        {
            std::string details = "Unknown terminate reason.";
            if (const std::exception_ptr currentException = std::current_exception(); currentException != nullptr)
            {
                try
                {
                    std::rethrow_exception(currentException);
                }
                catch (const std::exception& exception)
                {
                    details = std::string("Unhandled std::exception: ") + exception.what();
                }
                catch (...)
                {
                    details = "Unhandled non-standard exception.";
                }
            }

            WriteCrash("std::terminate", details);
            std::abort();
        }

#ifdef DEBUG
        static int __cdecl OnCrtReport(const int reportType, char* message, int*)
        {
            if (reportType == _CRT_ASSERT || reportType == _CRT_ERROR)
            {
                WriteCrash("CRT assert/report", message == nullptr ? "(empty CRT report)" : std::string(message));
            }

            return FALSE;
        }
#endif
    };
}
