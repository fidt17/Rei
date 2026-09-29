namespace Rei.EngineIntegration.Tests;

public sealed class EngineFactAttribute : FactAttribute
{
    public EngineFactAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "Real engine tests require Windows and a graphics session.";
        else if (Environment.GetEnvironmentVariable("REI_RUN_ENGINE_TESTS") != "1")
            Skip = "Set REI_RUN_ENGINE_TESTS=1 and REI_TEST_EDITOR_EXE to run real engine tests.";
    }
}
