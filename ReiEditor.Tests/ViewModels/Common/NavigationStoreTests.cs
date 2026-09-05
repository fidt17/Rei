using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Factory;
using ReiEditor.ViewModels.Common;

namespace ReiEditor.Tests.ViewModels.Common;

/// <summary>
/// Verifies navigation replacement, lifecycle notifications, logging, and factory failures.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Navigation")]
public sealed class NavigationStoreTests
{
    private sealed class TestPageViewModel : BaseViewModel
    {
        public int DisposeCalls { get; private set; }

        public override void Dispose() => DisposeCalls++;
    }

    private sealed class TestFactory(Func<TestPageViewModel> create) : IFactory<TestPageViewModel>
    {
        public int CreateCalls { get; private set; }

        public TestPageViewModel CreateInstance()
        {
            CreateCalls++;
            return create();
        }

        public TestPageViewModel CreateInstance(params object[] parameters) => throw new NotSupportedException();
    }

    /// <summary>
    /// Navigation replaces the initial empty view and disposes the previous page before publishing changes.
    /// </summary>
    [Fact]
    public void NavigateReplacesAndDisposesPreviousPageBeforeNotification()
    {
        using var store = new NavigationStore();
        var first = new TestPageViewModel();
        var second = new TestPageViewModel();
        var firstFactory = new TestFactory(() => first);
        var secondFactory = new TestFactory(() => second);
        var observed = new List<BaseViewModel>();
        var properties = new List<string?>();
        store.PropertyChanged += (_, args) => properties.Add(args.PropertyName);
        store.ChangedEvent += () =>
        {
            if (ReferenceEquals(store.ViewModel, second)) Assert.Equal(1, first.DisposeCalls);
            observed.Add(store.ViewModel);
        };
        Assert.IsType<EmptyViewModel>(store.ViewModel);

        Assert.Same(first, store.Navigate(firstFactory));
        Assert.Same(first, store.ViewModel);
        Assert.Equal(0, first.DisposeCalls);
        Assert.Same(second, store.Navigate(secondFactory));

        Assert.Same(second, store.ViewModel);
        Assert.Equal(1, first.DisposeCalls);
        Assert.Equal(0, second.DisposeCalls);
        Assert.Equal(new BaseViewModel[] { first, second }, observed);
        Assert.Equal(new[] { nameof(store.ViewModel), nameof(store.ViewModel) }, properties);
        Assert.Equal(1, firstFactory.CreateCalls);
        Assert.Equal(1, secondFactory.CreateCalls);
        second.Dispose();
    }

    /// <summary>
    /// Failed page creation preserves the active page and emits no replacement or property notifications.
    /// </summary>
    [Fact]
    public void FactoryFailurePreservesCurrentPage()
    {
        using var store = new NavigationStore();
        var current = new TestPageViewModel();
        store.Navigate(new TestFactory(() => current));
        var failure = new InvalidOperationException("page creation failed");
        var failingFactory = new TestFactory(() => throw failure);
        var notifications = 0;
        store.ChangedEvent += () => notifications++;
        store.PropertyChanged += (_, _) => notifications++;

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => store.Navigate(failingFactory)));

        Assert.Same(current, store.ViewModel);
        Assert.Equal(0, current.DisposeCalls);
        Assert.Equal(0, notifications);
        Assert.Equal(1, failingFactory.CreateCalls);
        current.Dispose();
    }

    /// <summary>
    /// Navigation logging waits for a change and uses the destination type with the view-model suffix removed.
    /// </summary>
    [Fact]
    public void LogOnNavigateReportsDestination()
    {
        using var store = new NavigationStore();
        var logger = new TestLogger<NavigationStore>();
        var page = new TestPageViewModel();
        store.LogOnNavigate(logger);
        Assert.Empty(logger.Entries);

        store.Navigate(new TestFactory(() => page));

        Assert.Equal("Navigate to TestPage", Assert.Single(logger.Entries).Message);
        page.Dispose();
    }
}
