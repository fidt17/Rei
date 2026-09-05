using ReiEditor.ViewModels.Controls;

namespace ReiEditor.Tests.ViewModels.Controls;

/// <summary>Verifies nested menu commands, current availability and independently cloned command graphs.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "ContextMenu")]
public sealed class ContextMenuViewModelTests
{
    /// <summary>Cloned commands preserve callbacks and dynamic predicates but notify only their owning menu tree.</summary>
    [Fact]
    public void CloneOwnsCommandsAndBubblesNestedExecution()
    {
        var enabled = false;
        var calls = 0;
        using var nested = new ContextMenuViewModel();
        nested.AddOption(new ContextMenuOption("Action", () => calls++, () => enabled, () => enabled ? "Ready" : "Blocked"));
        using var original = new ContextMenuViewModel();
        original.AddOption(new ContextMenuOption("Nested", nested));
        original.AddOption(ContextMenuOption.Separator());
        using var clone = original.Clone();
        var originalEvents = 0;
        var cloneEvents = 0;
        original.AnyCommandExecutedEvent += () => originalEvents++;
        clone.AnyCommandExecutedEvent += () => cloneEvents++;
        var action = Assert.Single(clone.Options[0].NestedMenu!.Options);
        Assert.NotSame(nested, clone.Options[0].NestedMenu);
        Assert.NotSame(nested.Options[0].Command, action.Command);
        Assert.False(action.IsEnabled);
        Assert.False(action.Command.CanExecute(null));
        Assert.Equal("Blocked", action.ToolTip);
        enabled = true;
        Assert.True(action.Command.CanExecute(null));
        Assert.Equal("Ready", action.ToolTip);
        action.Command.Execute(null);
        Assert.Equal(1, calls);
        Assert.Equal(1, cloneEvents);
        Assert.Equal(0, originalEvents);
        Assert.True(clone.Options[1].IsSeparator);
        Assert.False(clone.Options[0].ShouldCloseOnExecute);
        Assert.True(action.ShouldCloseOnExecute);
    }
}
