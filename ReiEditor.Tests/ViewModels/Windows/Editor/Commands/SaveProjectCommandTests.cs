using Avalonia.Headless.XUnit;
using ReiEditor.Models.Services.Scenes;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Commands;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Commands;

/// <summary>Verifies save command eligibility, awaited save ordering and notification lifetime.</summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Commands")]
public sealed class SaveProjectCommandTests
{
    private sealed class TestSynchronizer(List<string> calls) : ISceneStateSynchronizer
    {
        public void SynchronizeStateWithEngine() => calls.Add("sync");
    }

    /// <summary>Saving, playmode and build independently disable save; subscribed state changes stop notifying after disposal.</summary>
    [AvaloniaFact]
    public void GuardsAndSubscriptionsFollowDependencyState()
    {
        var assets = new TestAssetsService();
        var engine = new TestEngineRunner();
        var build = new TestBuildService();
        using var command = new SaveProjectCommand(assets, build, engine, new TestSynchronizer([]));
        var changes = 0;
        command.CanExecuteChanged += (_, _) => changes++;
        Assert.True(command.CanExecute(null));
        assets.Saving.Value = true;
        Assert.False(command.CanExecute(null));
        assets.Saving.Value = false;
        engine.PlaymodeActive.Value = true;
        Assert.False(command.CanExecute(null));
        engine.PlaymodeActive.Value = false;
        build.InProgress.Value = true;
        Assert.False(command.CanExecute(null));
        build.InProgress.Value = false;
        Assert.True(command.CanExecute(null));
        Assert.Equal(4, changes);
        Assert.Equal(1, engine.PlaymodeSubscriberCount);
        command.Dispose();
        Assert.Equal(0, engine.PlaymodeSubscriberCount);
        engine.PlaymodeActive.Value = true;
        build.InProgress.Value = true;
        Assert.Equal(4, changes);
    }

    /// <summary>Scene synchronization precedes save; success notifies after completion and failure propagates without success notification.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveCompletionControlsNotification(bool fail)
    {
        var calls = new List<string>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var assets = new TestAssetsService { OnSave = () => { calls.Add("save"); return gate.Task; } };
        using var command = new SaveProjectCommand(assets, new TestBuildService(), new TestEngineRunner(), new TestSynchronizer(calls));
        command.CanExecuteChanged += (_, _) => calls.Add("notify");
        var saving = command.SaveProject();
        var failure = new IOException("controlled save failure");
        try
        {
            Assert.Equal(new[] { "sync", "save" }, calls);
            Assert.False(saving.IsCompleted);
        }
        finally
        {
            if (fail) gate.TrySetException(failure);
            else gate.TrySetResult();
            if (fail) Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => saving));
            else await saving.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(fail ? new[] { "sync", "save" } : new[] { "sync", "save", "notify" }, calls);
    }
}
