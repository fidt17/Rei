#pragma once
#include "ProfileMarkers.h"
#include "glad/glad.h"
#include "GLFW/glfw3.h"
#include <array>
#include <cstdlib>

namespace rei::profiling
{
    // Opt-in GPU command timestamps. Results are read asynchronously, never with glFinish.
    // Ns / Samples gives GPU mean time; samples need not equal CPU capture frame count.
    class GpuTimer
    {
        struct Pending
        {
            u32 Start = 0;
            u32 End = 0;
            ProfilingService* Service = nullptr;
            u64 Capture = 0;
            bool Issued = false;
        };

    public:
        GpuTimer(u64 nanoseconds, u64 samples) : _nanoseconds(nanoseconds), _samples(samples)
        {
            const auto requested = std::getenv("REI_PROFILE_GPU");
            _enabled = requested && requested[0] == '1' && requested[1] == '\0';
        }

        ~GpuTimer() { Dispose(); }
        GpuTimer(const GpuTimer&) = delete;
        GpuTimer& operator=(const GpuTimer&) = delete;

        void Begin()
        {
            if (!_enabled) return;
            Poll();
            auto* service = ProfilingService::Current();
            if (!service) return;
            if (!glQueryCounter || !glGetQueryObjectui64v)
            {
                Count(markers::GPU_UNSUPPORTED.Id);
                return;
            }
            if (!_context)
            {
                _context = glfwGetCurrentContext();
                if (!_context) return;
                for (auto& pending : _pending)
                {
                    glGenQueries(1, &pending.Start);
                    glGenQueries(1, &pending.End);
                }
            }
            if (_context != glfwGetCurrentContext()) return;
            for (u32 i = 0; i < _pending.size(); ++i)
            {
                auto& pending = _pending[i];
                if (pending.Issued) continue;
                pending.Service = service;
                pending.Capture = service->GetCaptureToken();
                pending.Issued = true;
                _active = static_cast<i32>(i);
                glQueryCounter(pending.Start, GL_TIMESTAMP);
                return;
            }
            Count(markers::GPU_SKIPPED.Id);
        }

        void End()
        {
            if (_active < 0) return;
            glQueryCounter(_pending[_active].End, GL_TIMESTAMP);
            _active = -1;
        }

        void Poll()
        {
            if (!_context || _context != glfwGetCurrentContext()) return;
            for (auto& pending : _pending)
            {
                if (!pending.Issued) continue;
                i32 available = 0;
                glGetQueryObjectiv(pending.End, GL_QUERY_RESULT_AVAILABLE, &available);
                if (!available) continue;
                u64 start = 0, end = 0;
                glGetQueryObjectui64v(pending.Start, GL_QUERY_RESULT, &start);
                glGetQueryObjectui64v(pending.End, GL_QUERY_RESULT, &end);
                auto* service = ProfilingService::Current();
                if (service && service == pending.Service && service->GetCaptureToken() == pending.Capture && end >= start)
                {
                    Count(_nanoseconds, end - start);
                    Count(_samples);
                }
                pending.Issued = false;
            }
        }

        void Dispose()
        {
            if (_context && _context == glfwGetCurrentContext())
            {
                for (auto& pending : _pending)
                {
                    glDeleteQueries(1, &pending.Start);
                    glDeleteQueries(1, &pending.End);
                }
            }
            _pending = {};
            _context = nullptr;
            _active = -1;
        }

        class Scope
        {
        public:
            explicit Scope(GpuTimer& timer) : _timer(timer) { _timer.Begin(); }
            ~Scope() { _timer.End(); }
        private:
            GpuTimer& _timer;
        };

    private:
        static constexpr u32 MAX_PENDING = 16;
        std::array<Pending, MAX_PENDING> _pending{};
        GLFWwindow* _context = nullptr;
        u64 _nanoseconds;
        u64 _samples;
        i32 _active = -1;
        bool _enabled = false;
    };
}
