using ReiEditor.Models.Services.Build;
using ReiEditor.Tests.Infrastructure.TestDoubles;

namespace ReiEditor.Tests.Models.Services.Build;

/// <summary>Verifies saving is awaited and cancellation is observed on both sides of build preparation.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Build")]
public sealed class BuildPreparationServiceTests
{
    /// <summary>Preparation remains pending until project saving completes.</summary>
    [Fact]
    public async Task AwaitsSaveCompletion()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saves = 0;
        var service = new BuildPreparationService(new TestAssetsService { OnSave = () => { saves++; return gate.Task; } });
        var preparation = service.Prepare(CancellationToken.None);
        try
        {
            Assert.Equal(1, saves);
            Assert.False(preparation.IsCompleted);
            gate.SetResult();
            await preparation.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            gate.TrySetResult();
            await preparation.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Cancellation before preparation skips saving; cancellation during saving prevents preparation success.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ObservesCancellationBeforeAndAfterSave(bool canceledBefore)
    {
        using var cancellation = new CancellationTokenSource();
        var saves = 0;
        var service = new BuildPreparationService(new TestAssetsService
        {
            OnSave = () => { saves++; cancellation.Cancel(); return Task.CompletedTask; }
        });
        if (canceledBefore) cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.Prepare(cancellation.Token));

        Assert.Equal(canceledBefore ? 0 : 1, saves);
    }

    /// <summary>Save errors propagate to the build orchestrator unchanged.</summary>
    [Fact]
    public async Task PropagatesSaveFailure()
    {
        var failure = new IOException("controlled save failure");
        var service = new BuildPreparationService(new TestAssetsService { OnSave = () => Task.FromException(failure) });
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => service.Prepare(CancellationToken.None)));
    }
}
