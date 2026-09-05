using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Tests.Infrastructure.Builders;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;

namespace ReiEditor.Tests.ViewModels;

/// <summary>
/// Verifies property editor notifications and subscription lifetime on the Avalonia UI thread.
/// </summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "PropertyEditors")]
public sealed class IntegerPropertyViewModelTests
{
    /// <summary>
    /// A property update explicitly dispatched to the UI thread notifies the view model once; disposal stops later updates.
    /// </summary>
    [AvaloniaFact]
    public async Task PropertyUpdateOnDispatcherNotifiesViewModelAndDisposeUnsubscribes()
    {
        Assert.True(Dispatcher.UIThread.CheckAccess());
        var property = SerializedPropertyBuilder.Integer(value: 3);
        var viewModel = new IntegerPropertyViewModel(property);
        var notification = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var notifications = 0;
        void RecordChange(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(IntegerPropertyViewModel.Value)) return;
            notifications++;
            notification.TrySetResult(Dispatcher.UIThread.CheckAccess());
        }

        viewModel.PropertyChanged += RecordChange;
        try
        {
            Assert.Equal(3, viewModel.Value);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await Task.Run(async () =>
            {
                await Dispatcher.UIThread.InvokeAsync(() => property.Value = 17, DispatcherPriority.Normal, timeout.Token);
            }).WaitAsync(timeout.Token);

            Assert.True(await notification.Task.WaitAsync(timeout.Token));
            Assert.Equal(17, viewModel.Value);
            Assert.Equal(1, notifications);
        }
        finally
        {
            viewModel.PropertyChanged -= RecordChange;
            viewModel.Dispose();
        }

        property.Value = 23;
        Assert.Equal(17, viewModel.Value);
    }
}
