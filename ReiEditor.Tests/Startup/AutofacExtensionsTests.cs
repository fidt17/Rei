using Autofac;
using Autofac.Core;
using ReiEditor.Utils.Extensions;

namespace ReiEditor.Tests.Startup;

/// <summary>Verifies singleton and eager-registration helpers without starting application scopes.</summary>
[Trait("Category", "Composition")]
[Trait("Area", "Startup")]
public sealed class AutofacExtensionsTests
{
    private interface ITestService;
    private sealed class TestSingleton : ITestService, IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }
    private sealed class TestCreationTracker
    {
        public int Created { get; set; }
        public int Disposed { get; set; }
    }
    private sealed class TestEagerService : IDisposable
    {
        private readonly TestCreationTracker _tracker;
        public TestEagerService(TestCreationTracker tracker)
        {
            _tracker = tracker;
            tracker.Created++;
        }
        public void Dispose() => _tracker.Disposed++;
    }

    /// <summary>Singleton registration exposes one instance through chained service aliases and descendant scopes.</summary>
    [Fact]
    public void SingletonAliasesShareOneOwnedInstance()
    {
        var builder = new ContainerBuilder();
        builder.RegisterSingleton<TestSingleton>().As<ITestService>().AsSelf();
        var container = builder.Build();
        TestSingleton instance;
        try
        {
            using var child = container.BeginLifetimeScope();
            instance = container.Resolve<TestSingleton>();
            Assert.Same(instance, container.Resolve<ITestService>());
            Assert.Same(instance, child.Resolve<ITestService>());
            Assert.Equal(0, instance.DisposeCount);
        }
        finally
        {
            container.Dispose();
        }
        Assert.Equal(1, instance.DisposeCount);
    }

    /// <summary>Non-lazy registration creates during Build and preserves its chosen transient or singleton lifetime.</summary>
    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 1)]
    public void NonLazyConstructionUsesConfiguredLifetime(bool singleton, int expectedCreated)
    {
        var tracker = new TestCreationTracker();
        var builder = new ContainerBuilder();
        builder.RegisterInstance(tracker);
        var registration = builder.RegisterNonLazy<TestEagerService>();
        if (singleton) registration.SingleInstance();
        Assert.Equal(0, tracker.Created);
        using (var container = builder.Build())
        {
            Assert.Equal(1, tracker.Created);
            var first = container.Resolve<TestEagerService>();
            var second = container.Resolve<TestEagerService>();
            Assert.Equal(singleton, ReferenceEquals(first, second));
            Assert.Equal(expectedCreated, tracker.Created);
        }
        Assert.Equal(expectedCreated, tracker.Disposed);
    }

    /// <summary>Missing eager dependencies fail container Build rather than hiding the failure until later resolution.</summary>
    [Fact]
    public void NonLazyMissingDependencyFailsBuild()
    {
        var builder = new ContainerBuilder();
        builder.RegisterNonLazy<TestEagerService>();
        Assert.Throws<DependencyResolutionException>(() => builder.Build());
    }
}
