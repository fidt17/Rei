using ReiEditor.Utils.Common;
using ReiEditor.Utils.Common.Condition;

namespace ReiEditor.Tests.Utils.Common;

/// <summary>
/// Verifies boolean target matching and conjunction lifecycle without UI infrastructure.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Common")]
public sealed class ConditionTests
{
    private sealed class TestCondition(bool initial) : ICondition
    {
        public Observable<bool> State { get; } = new(initial);
        public ReiEditor.Utils.Common.IObservable<bool> IsTrue => State;
        public int DisposeCalls { get; private set; }

        public void Dispose() => DisposeCalls++;
    }

    /// <summary>
    /// A condition evaluates its initial source and follows transitions for either target.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ConditionMatchesTargetAndTracksChanges(bool initial, bool target)
    {
        var source = new Observable<bool>(initial);
        using var condition = new Condition(source, target);

        Assert.Equal(initial == target, condition.IsTrue.Value);
        source.Value = !initial;

        Assert.Equal(!initial == target, condition.IsTrue.Value);
    }

    /// <summary>
    /// The default target is false and disposing disconnects subsequent source changes.
    /// </summary>
    [Fact]
    public void ConditionDisposeDisconnectsSource()
    {
        var source = new Observable<bool>(false);
        var condition = new Condition(source);
        var values = new List<bool>();
        condition.IsTrue.Subscribe(values.Add, invoke: false);
        Assert.True(condition.IsTrue.Value);

        condition.Dispose();
        source.Value = true;

        Assert.True(condition.IsTrue.Value);
        Assert.Empty(values);
        condition.IsTrue.Unsubscribe(values.Add);
    }

    /// <summary>
    /// An empty conjunction is true.
    /// </summary>
    [Fact]
    public void EmptyGroupIsTrue()
    {
        using var group = new ConditionGroup();

        Assert.True(group.IsTrue.Value);
    }

    /// <summary>
    /// A group requires every child and publishes only changes in its combined result.
    /// </summary>
    [Fact]
    public void GroupPublishesConjunctionChangesOnly()
    {
        var first = new TestCondition(false);
        var second = new TestCondition(false);
        using var group = new ConditionGroup(first, second);
        var values = new List<bool>();
        group.IsTrue.Subscribe(values.Add, invoke: false);
        Assert.False(group.IsTrue.Value);

        first.State.Value = true;
        Assert.False(group.IsTrue.Value);
        Assert.Empty(values);
        second.State.Value = true;
        Assert.True(group.IsTrue.Value);
        first.State.Value = false;
        second.State.Value = false;

        Assert.False(group.IsTrue.Value);
        Assert.Equal(new[] { true, false }, values);
        group.IsTrue.Unsubscribe(values.Add);
    }

    /// <summary>
    /// Group disposal releases each child once and removes callbacks even across repeated disposal.
    /// </summary>
    [Fact]
    public void GroupDisposeReleasesChildrenAndDisconnectsTheirChanges()
    {
        var first = new TestCondition(true);
        var second = new TestCondition(true);
        var group = new ConditionGroup(first, second);
        var values = new List<bool>();
        group.IsTrue.Subscribe(values.Add, invoke: false);

        group.Dispose();
        first.State.Value = false;
        second.State.Value = false;
        group.Dispose();

        Assert.Equal(1, first.DisposeCalls);
        Assert.Equal(1, second.DisposeCalls);
        Assert.Empty(values);
        group.IsTrue.Unsubscribe(values.Add);
    }
}
