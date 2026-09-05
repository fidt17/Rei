using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Utils.Common;

/// <summary>
/// Verifies pool creation, reset, reuse, and delegate failure propagation in single-threaded use.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Common")]
public sealed class PoolTests
{
    /// <summary>
    /// Empty retrieval creates and resets an object, and returning it makes the same instance reusable.
    /// </summary>
    [Fact]
    public void GetCreatesResetsAndReusesReturnedObject()
    {
        var factoryCalls = 0;
        var resets = new List<object>();
        var pool = new Pool<object>(() =>
        {
            factoryCalls++;
            return new object();
        }, resets.Add);

        var first = pool.Get();
        Assert.Equal(1, factoryCalls);
        Assert.Same(first, Assert.Single(resets));
        pool.Put(first);
        Assert.Equal(2, resets.Count);
        Assert.Same(first, resets[1]);
        var reused = pool.Get();

        Assert.Same(first, reused);
        Assert.Equal(1, factoryCalls);
        Assert.Equal(2, resets.Count);
    }

    /// <summary>
    /// Population creates and resets exactly the requested objects before any are borrowed.
    /// </summary>
    [Fact]
    public void PopulateMakesEveryCreatedObjectAvailable()
    {
        var created = new List<object>();
        var resets = new List<object>();
        var pool = new Pool<object>(() =>
        {
            var value = new object();
            created.Add(value);
            return value;
        }, resets.Add);

        pool.Populate(3);
        Assert.Equal(3, created.Count);
        Assert.Equal(created, resets);
        var borrowed = new[] { pool.Get(), pool.Get(), pool.Get() };

        Assert.Equal(3, created.Count);
        Assert.Equal(3, resets.Count);
        Assert.Equal(3, borrowed.Distinct().Count());
        Assert.All(created, value => Assert.Contains(value, borrowed));
    }

    /// <summary>
    /// Nonpositive population does not invoke creation or reset delegates.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NonpositivePopulationDoesNothing(int count)
    {
        var pool = new Pool<object>(
            () => throw new InvalidOperationException("Unexpected creation"),
            _ => throw new InvalidOperationException("Unexpected reset"));

        pool.Populate(count);
    }

    /// <summary>
    /// Creation failures propagate without invoking reset and a later successful retrieval can recover.
    /// </summary>
    [Fact]
    public void FactoryFailurePropagatesAndLaterGetCanRecover()
    {
        var failure = new InvalidOperationException("creation failed");
        var shouldFail = true;
        var value = new object();
        var resets = new List<object>();
        var pool = new Pool<object>(() => shouldFail ? throw failure : value, resets.Add);

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => pool.Get()));
        Assert.Empty(resets);
        shouldFail = false;

        Assert.Same(value, pool.Get());
        Assert.Same(value, Assert.Single(resets));
    }

    /// <summary>
    /// Returning an object propagates reset errors to the caller.
    /// </summary>
    [Fact]
    public void PutPropagatesResetFailure()
    {
        var failure = new InvalidOperationException("reset failed");
        var pool = new Pool<object>(() => new object(), _ => throw failure);

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => pool.Put(new object())));
    }
}
