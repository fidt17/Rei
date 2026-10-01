#include "Startup/AppEntryPoint.h"
#include <Common/Profiling/ProfilingService.h>
#include <thread>

namespace
{
    constexpr auto UPDATE = rei::profiling::MakeScope("Fixture.Profiling.Update");
    constexpr auto WAIT = rei::profiling::MakeScope("Fixture.Profiling.Wait");
    constexpr auto COUNT = rei::profiling::MakeCounter("Fixture.Profiling.Count");
    constexpr std::array DESCRIPTORS = {UPDATE, WAIT, COUNT};
}
class TestApplication final : public rei::App
{
public:
    void OnStart() override { rei::GetProfiler().Register(DESCRIPTORS); }
    void OnUpdate() override
    {
        REI_PROFILE_SCOPE(UPDATE.Id);
        rei::profiling::Count(COUNT.Id, 3);
        if (rei::profiling::ProfilingService::Current())
        {
            REI_PROFILE_SCOPE(WAIT.Id);
            // Only the isolated fixture slows active captures, making busy/Stop checks bounded and deterministic.
            std::this_thread::sleep_for(std::chrono::milliseconds(1));
        }
    }
    void OnShutdown() override { }
};
std::shared_ptr<rei::App> CreateApp() { return std::make_shared<TestApplication>(); }
