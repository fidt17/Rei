namespace Rei.EngineIntegration.Tests;

// Separate collection lets xUnit dispose the shared smoke Editor before lifecycle cases run.
[CollectionDefinition(NAME, DisableParallelization = true)]
public sealed class EngineLifecycleCollection
{
    public const string NAME = "Real engine lifecycle";
}
