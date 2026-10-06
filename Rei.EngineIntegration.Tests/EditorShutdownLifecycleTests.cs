using Xunit.Abstractions;

namespace Rei.EngineIntegration.Tests;

[Collection(EngineLifecycleCollection.NAME)]
[Trait("Category", "EngineIntegration")]
[Trait("Suite", "Lifecycle")]
public sealed class EditorShutdownLifecycleTests(ITestOutputHelper output)
{
    [EngineFact]
    public async Task ClosingEditorStopsEngineAndDisposesEditorWithoutLateHierarchyErrors()
    {
        await using var engine = new EngineIntegrationHarness();
        output.WriteLine($"Artifacts: {engine.RunDirectory}");
        await engine.StartAsync();
        var logs = await engine.CloseEditorAsync();
        Assert.Contains("Shutdown complete", logs);
        Assert.DoesNotContain("ObjectDisposedException", logs);
        Assert.DoesNotContain("Exception:", logs);
        Assert.DoesNotContain("[ERROR]", logs);
    }
}
