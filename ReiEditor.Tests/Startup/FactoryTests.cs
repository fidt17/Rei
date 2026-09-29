using Autofac;
using Autofac.Core;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.Utils.Factory;

namespace ReiEditor.Tests.Startup;

/// <summary>Verifies factory resolution, typed arguments, container lifetimes and failure logging.</summary>
[Trait("Category", "Composition")]
[Trait("Area", "Startup")]
public sealed class FactoryTests
{
    private interface ITestDependency;
    private sealed class TestDependency : ITestDependency;
    private sealed class TestProduct(ITestDependency dependency, string name, int count)
    {
        public ITestDependency Dependency { get; } = dependency;
        public string Name { get; } = name;
        public int Count { get; } = count;
    }
    private sealed class TestDesignProduct
    {
        public ITestDependency? Dependency { get; }
        public TestDesignProduct() { }
        public TestDesignProduct(ITestDependency dependency) => Dependency = dependency;
    }

    private sealed class TestOwnedResource : IDisposable
    {
        public int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }

    /// <summary>Runtime arguments match exact constructor types while other dependencies come from the container.</summary>
    [Fact]
    public void TypedArgumentsMixWithRegisteredDependencies()
    {
        var dependency = new TestDependency();
        var builder = new ContainerBuilder();
        builder.RegisterInstance(dependency).As<ITestDependency>();
        builder.RegisterType<TestProduct>();
        using var container = builder.Build();
        var logger = new TestLogger<Factory<TestProduct>>();
        var factory = new Factory<TestProduct>(container, logger);
        var product = factory.CreateInstance(12, "named");
        Assert.Same(dependency, product.Dependency);
        Assert.Equal("named", product.Name);
        Assert.Equal(12, product.Count);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void InterfaceArgumentDoesNotFallBackToDesignConstructor()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<TestDesignProduct>();
        using var container = builder.Build();
        var factory = new Factory<TestDesignProduct>(container, new TestLogger<Factory<TestDesignProduct>>());
        var dependency = new TestDependency();

        Assert.Same(dependency, factory.CreateInstance(dependency).Dependency);
    }

    /// <summary>The factory respects singleton ownership and the container disposes its returned object once.</summary>
    [Fact]
    public void ResolutionPreservesContainerLifetimeAndOwnership()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<TestOwnedResource>().SingleInstance();
        var container = builder.Build();
        TestOwnedResource resource;
        try
        {
            var factory = new Factory<TestOwnedResource>(container, new TestLogger<Factory<TestOwnedResource>>());
            resource = factory.CreateInstance();
            Assert.Same(resource, factory.CreateInstance());
            Assert.Equal(0, resource.DisposeCount);
        }
        finally
        {
            container.Dispose();
        }
        Assert.Equal(1, resource.DisposeCount);
    }

    /// <summary>Missing registration failures are logged once and rethrown unchanged for either overload.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolutionFailureIsLoggedAndRethrown(bool parameters)
    {
        using var container = new ContainerBuilder().Build();
        var logger = new TestLogger<Factory<TestProduct>>();
        var factory = new Factory<TestProduct>(container, logger);
        var failure = Assert.Throws<Autofac.Core.Registration.ComponentNotRegisteredException>(() => parameters ? factory.CreateInstance("name") : factory.CreateInstance());
        Assert.Same(failure, Assert.Single(logger.Entries).Exception);
    }

    /// <summary>Runtime arguments bind to assignable interface parameters without container registration.</summary>
    [Fact]
    public void RuntimeArgumentBindsToInterfaceParameter()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<TestProduct>();
        using var container = builder.Build();
        var logger = new TestLogger<Factory<TestProduct>>();
        var factory = new Factory<TestProduct>(container, logger);
        var dependency = new TestDependency();
        var product = factory.CreateInstance(dependency, "name", 2);
        Assert.Same(dependency, product.Dependency);
        Assert.Equal("name", product.Name);
        Assert.Equal(2, product.Count);
        Assert.Empty(logger.Entries);
    }
}
