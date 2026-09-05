using Autofac;
using ReiEditor.Startup.Common;

namespace ReiEditor.Tests.Startup;

/// <summary>Verifies scope resolution, startup task propagation and ordered asynchronous ownership cleanup.</summary>
[Trait("Category", "Composition")]
[Trait("Area", "Startup")]
public sealed class BaseLifetimeScopeTests
{
    private sealed class TestRootToken;

    private sealed class TestResource(string name, List<string> disposals) : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }
        public Func<Task> OnDispose { get; set; } = () => Task.CompletedTask;
        public async ValueTask DisposeAsync()
        {
            DisposeCount++;
            await OnDispose();
            disposals.Add(name);
        }
    }

    private class TestScope : BaseLifetimeScope
    {
        public string Name { get; }
        public List<string> Disposals { get; }
        public Func<Task> OnStart { get; set; } = () => Task.CompletedTask;
        public int StartCount { get; private set; }

        public TestScope(string name, List<string> disposals) : base(name)
        {
            Name = name;
            Disposals = disposals;
        }

        public TestScope(string name, List<string> disposals, BaseLifetimeScope parent) : base(name, parent)
        {
            Name = name;
            Disposals = disposals;
        }

        protected override void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterInstance(this).ExternallyOwned();
            // Configure runs in the base constructor; resolve this deferred factory only after construction.
            builder.Register(_ => new TestResource(Name, Disposals)).SingleInstance();
        }

        protected override Task OnScopeStart()
        {
            StartCount++;
            return OnStart();
        }
    }

    private sealed class TestRootScope(List<string> disposals) : TestScope("root", disposals)
    {
        protected override void ConfigureContainer(ContainerBuilder builder)
        {
            base.ConfigureContainer(builder);
            builder.Register(_ => new TestRootToken()).SingleInstance();
        }
    }

    /// <summary>Children inherit parent registrations while resolving their own resources; stopping a child keeps its parent usable.</summary>
    [Fact]
    public async Task ChildResolutionAndLifetimeAreIsolatedFromParent()
    {
        var disposals = new List<string>();
        var root = new TestRootScope(disposals);
        try
        {
            var child = new TestScope("child", disposals, root);
            var parentResource = root.Scope.Resolve<TestResource>();
            var childResource = child.Scope.Resolve<TestResource>();
            Assert.Same(root.Scope.Resolve<TestRootToken>(), child.Scope.Resolve<TestRootToken>());
            Assert.Same(child, child.Scope.Resolve<TestScope>());
            Assert.NotSame(parentResource, childResource);
            Assert.Same(childResource, child.Scope.Resolve<TestResource>());

            await child.StopAsync();

            Assert.Equal(new[] { "child" }, disposals);
            Assert.Equal(1, childResource.DisposeCount);
            Assert.Equal(0, parentResource.DisposeCount);
            Assert.Throws<ObjectDisposedException>(() => child.Scope.Resolve<TestResource>());
            Assert.Same(parentResource, root.Scope.Resolve<TestResource>());
        }
        finally
        {
            await root.StopAsync();
        }
        Assert.Equal(new[] { "child", "root" }, disposals);
    }

    /// <summary>Parent stop drains children in reverse creation order and descendants before their owner.</summary>
    [Fact]
    public async Task StopDisposesNestedScopesInReverseOwnershipOrder()
    {
        var disposals = new List<string>();
        var root = new TestRootScope(disposals);
        try
        {
            var first = new TestScope("first", disposals, root);
            var grandchild = new TestScope("grandchild", disposals, first);
            var second = new TestScope("second", disposals, root);
            foreach (var scope in new TestScope[] { root, first, grandchild, second }) scope.Scope.Resolve<TestResource>();
            await root.StopAsync();
            Assert.Equal(new[] { "second", "grandchild", "first", "root" }, disposals);
        }
        finally
        {
            await root.StopAsync();
        }
    }

    /// <summary>Startup returns the hook task and remains incomplete until its controlled initializer finishes.</summary>
    [Fact]
    public async Task StartWaitsForInitializationHook()
    {
        var scope = new TestScope("start", []);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        scope.OnStart = () => gate.Task;
        var start = scope.StartAsync();
        try
        {
            Assert.Equal(1, scope.StartCount);
            Assert.Same(gate.Task, start);
            Assert.False(start.IsCompleted);
        }
        finally
        {
            gate.TrySetResult();
            await start.WaitAsync(TimeSpan.FromSeconds(5));
            await scope.StopAsync();
        }
    }

    /// <summary>Both synchronous and asynchronous startup failures preserve the original exception for the caller.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupFailurePropagates(bool asynchronous)
    {
        var scope = new TestScope("failure", []);
        var failure = new InvalidOperationException("controlled startup failure");
        scope.OnStart = asynchronous ? () => Task.FromException(failure) : () => throw failure;
        try
        {
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => scope.StartAsync()));
            Assert.Equal(1, scope.StartCount);
        }
        finally
        {
            await scope.StopAsync();
        }
    }

    /// <summary>Parent disposal waits for a child's asynchronous disposal before releasing parent resources.</summary>
    [Fact]
    public async Task StopWaitsForChildDisposal()
    {
        var disposals = new List<string>();
        var root = new TestRootScope(disposals);
        var child = new TestScope("child", disposals, root);
        var parentResource = root.Scope.Resolve<TestResource>();
        var childResource = child.Scope.Resolve<TestResource>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        childResource.OnDispose = () => { entered.TrySetResult(); return release.Task; };
        var stopping = root.StopAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(stopping.IsCompleted);
            Assert.Equal(0, parentResource.DisposeCount);
            Assert.Empty(disposals);
        }
        finally
        {
            release.TrySetResult();
            await stopping.WaitAsync(TimeSpan.FromSeconds(5));
            await root.StopAsync();
        }
        Assert.Equal(new[] { "child", "root" }, disposals);
    }

    /// <summary>Resource disposal errors propagate and do not silently report successful scope shutdown.</summary>
    [Fact]
    public async Task DisposalFailurePropagates()
    {
        var scope = new TestScope("dispose-failure", []);
        var resource = scope.Scope.Resolve<TestResource>();
        var failure = new InvalidOperationException("controlled disposal failure");
        resource.OnDispose = () => Task.FromException(failure);
        try
        {
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => scope.StopAsync()));
            Assert.Equal(1, resource.DisposeCount);
        }
        finally
        {
            // This resource owns no external state; the Autofac scope has already entered disposal.
            resource.OnDispose = () => Task.CompletedTask;
            await scope.StopAsync();
        }
    }
}
