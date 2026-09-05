using ReiEditor.Utils;

namespace ReiEditor.Tests.Utils;

/// <summary>
/// Verifies command delegates and execution and availability events.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Common")]
public sealed class RelayCommandTests
{
    /// <summary>
    /// Availability defaults to true and reevaluates the supplied predicate on each query.
    /// </summary>
    [Fact]
    public void CanExecuteUsesCurrentPredicateResult()
    {
        var allowed = false;
        var calls = 0;
        var command = new RelayCommand(canExecuteFunction: () =>
        {
            calls++;
            return allowed;
        });

        Assert.True(new RelayCommand().CanExecute(null));
        Assert.False(command.CanExecute(new object()));
        allowed = true;
        Assert.True(command.CanExecute(null));
        Assert.Equal(2, calls);
    }

    /// <summary>
    /// Execution completes its action before publishing completion, including commands without actions.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExecutePublishesAfterOptionalAction(bool hasAction)
    {
        var calls = new List<string>();
        var command = new RelayCommand(hasAction ? () => calls.Add("action") : null);
        command.ExecutedEvent += () => calls.Add("event");

        command.Execute(new object());

        Assert.Equal(hasAction ? new[] { "action", "event" } : new[] { "event" }, calls);
    }

    /// <summary>
    /// A failed action propagates its exception and does not publish successful execution.
    /// </summary>
    [Fact]
    public void ExecuteFailureDoesNotPublishExecutedEvent()
    {
        var failure = new InvalidOperationException("action failed");
        var command = new RelayCommand(() => throw failure);
        var published = false;
        command.ExecutedEvent += () => published = true;

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => command.Execute(null)));
        Assert.False(published);
    }

    /// <summary>
    /// Availability notifications identify the command and use the empty event arguments.
    /// </summary>
    [Fact]
    public void AvailabilityEventUsesCommandAsSender()
    {
        var command = new RelayCommand();
        var events = new List<(object? Sender, EventArgs Args)>();
        command.CanExecuteChanged += (sender, args) => events.Add((sender, args));

        command.InvokeCanExecuteChanged();

        var notification = Assert.Single(events);
        Assert.Same(command, notification.Sender);
        Assert.Same(EventArgs.Empty, notification.Args);
    }
}
