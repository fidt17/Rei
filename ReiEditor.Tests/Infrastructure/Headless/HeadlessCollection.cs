namespace ReiEditor.Tests.Infrastructure.Headless;

[CollectionDefinition(NAME, DisableParallelization = true)]
public sealed class HeadlessCollection
{
    public const string NAME = "Avalonia UI thread";
}
