using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Utils.Common.Procedures;
using ReiEditor.ViewModels.Windows.Editor;

namespace ReiEditor.Tests.ViewModels.Windows.Editor;

/// <summary>Verifies interaction blocking by procedure tags and event detachment without elapsed-time assertions.</summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Overlay")]
public sealed class EditorInteractionOverlayViewModelTests
{
    /// <summary>Each blocking tag displays the overlay; completion eventually restores interaction and disposal detaches new work.</summary>
    [AvaloniaTheory]
    [InlineData(ProcedureTags.SAVE_PROJECT)]
    [InlineData(ProcedureTags.IMPORT_ASSETS)]
    [InlineData(ProcedureTags.BUILD_PROJECT)]
    public async Task BlockingProceduresControlInteraction(string tag)
    {
        var service = new EditorProceduresService();
        var procedure = new Procedure(tag);
        service.TrackProcedure(procedure);
        using var vm = new EditorInteractionOverlayViewModel(service);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Assert.True(vm.IsVisible);
        Assert.False(vm.CanInteract);
        var hidden = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(vm.IsVisible) && !vm.IsVisible) hidden.TrySetResult(); };
        procedure.Complete();
        await hidden.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(vm.CanInteract);
        vm.Dispose();
        service.TrackProcedure(new Procedure(tag));
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Assert.False(vm.IsVisible);
    }
}
