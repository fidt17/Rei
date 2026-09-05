using ReiEditor.Models.EditorApp.EditorProcedures;
using ReiEditor.Utils.Common.Procedures;
using ReiEditor.ViewModels.Windows.Editor.StatusBar;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.StatusBar;

/// <summary>Verifies active procedure display, completion order and disposal boundaries.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "StatusBar")]
public sealed class StatusBarViewModelTests
{
    /// <summary>Latest procedure wins; any completion selects the newest remaining procedure, then clears status.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CompletionOrderPreservesRemainingProcedure(bool newestFirst)
    {
        var service = new EditorProceduresService();
        using var vm = new StatusBarViewModel(service);
        Assert.False(vm.ShowStatusBar);
        var first = new Procedure("First");
        var second = new Procedure("Second");
        service.TrackProcedure(first);
        service.TrackProcedure(second);
        Assert.True(vm.ShowStatusBar);
        Assert.Equal("Second...", vm.ActiveProcedureText);
        (newestFirst ? second : first).Complete();
        Assert.True(vm.ShowStatusBar);
        Assert.Equal(newestFirst ? "First..." : "Second...", vm.ActiveProcedureText);
        (newestFirst ? first : second).Complete();
        Assert.False(vm.ShowStatusBar);
        Assert.Empty(vm.ActiveProcedureText);
    }

    /// <summary>Disposal prevents notifications from procedures already tracked by the view model.</summary>
    [Fact]
    public void DisposeDetachesRunningProcedureCompletion()
    {
        var service = new EditorProceduresService();
        var vm = new StatusBarViewModel(service);
        var procedure = new Procedure("Held");
        service.TrackProcedure(procedure);
        vm.Dispose();
        var changes = new List<string?>();
        vm.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        procedure.Complete();
        service.TrackProcedure(new Procedure("Later"));
        Assert.Empty(changes);
    }
}
