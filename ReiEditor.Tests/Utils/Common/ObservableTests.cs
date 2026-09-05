using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Utils.Common;

/// <summary>
/// Verifies observable value delivery, equality suppression, and subscription lifecycle.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Common")]
public sealed class ObservableTests
{
    /// <summary>
    /// Subscription optionally publishes the current value and always receives later changes.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SubscribeControlsInitialDelivery(bool invoke)
    {
        var observable = new Observable<int>(7);
        var received = new List<int>();
        observable.Subscribe(received.Add, invoke);

        Assert.Equal(invoke ? new[] { 7 } : Array.Empty<int>(), received);
        observable.Value = 11;

        Assert.Equal(invoke ? new[] { 7, 11 } : new[] { 11 }, received);
        observable.Unsubscribe(received.Add);
    }

    /// <summary>
    /// Equal values are suppressed while forced publication includes the current stored value.
    /// </summary>
    [Fact]
    public void EqualAssignmentIsSilentButSetAndInvokePublishes()
    {
        var observable = new Observable<string>("value");
        var received = new List<string>();
        observable.Subscribe(received.Add, invoke: false);

        observable.Value = new string("value".ToCharArray());
        Assert.Empty(received);
        observable.SetAndInvoke("value");

        Assert.Equal(new[] { "value" }, received);
        string value = observable;
        Assert.Equal("value", value);
        observable.Unsubscribe(received.Add);
    }

    /// <summary>
    /// Subscribers observe the new stored value, including a transition to null.
    /// </summary>
    [Fact]
    public void NullTransitionUpdatesStateBeforeNotification()
    {
        var observable = new Observable<string?>("before");
        var received = new List<string?>();
        void Record(string? value)
        {
            Assert.Equal(value, observable.Value);
            received.Add(value);
        }
        observable.Subscribe(Record, invoke: false);

        observable.Value = null;
        observable.Value = "after";

        Assert.Equal(new string?[] { null, "after" }, received);
        observable.Unsubscribe(Record);
    }

    /// <summary>
    /// Removing a subscriber repeatedly stops its delivery without affecting other subscribers.
    /// </summary>
    [Fact]
    public void UnsubscribeRemovesOnlyItsCallbackAndAllowsResubscription()
    {
        var observable = new Observable<int>(0);
        var first = new List<int>();
        var second = new List<int>();
        observable.Subscribe(first.Add, invoke: false);
        observable.Subscribe(second.Add, invoke: false);

        observable.Unsubscribe(first.Add);
        observable.Unsubscribe(first.Add);
        observable.Value = 1;
        Assert.Empty(first);
        Assert.Equal(new[] { 1 }, second);

        observable.Subscribe(first.Add, invoke: false);
        observable.Value = 2;
        Assert.Equal(new[] { 2 }, first);
        Assert.Equal(new[] { 1, 2 }, second);
        observable.Unsubscribe(first.Add);
        observable.Unsubscribe(second.Add);
    }

    /// <summary>
    /// A callback can unsubscribe itself during publication without losing other deliveries.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SubscriberCanRemoveItselfDuringNotification(bool subscribeSelfFirst)
    {
        var observable = new Observable<int>(0);
        var selfCalls = 0;
        var otherValues = new List<int>();
        void RemoveSelf(int value)
        {
            selfCalls++;
            observable.Unsubscribe(RemoveSelf);
        }
        if (subscribeSelfFirst) observable.Subscribe(RemoveSelf, invoke: false);
        observable.Subscribe(otherValues.Add, invoke: false);
        if (!subscribeSelfFirst) observable.Subscribe(RemoveSelf, invoke: false);

        observable.Value = 1;
        observable.Value = 2;

        Assert.Equal(1, selfCalls);
        Assert.Equal(new[] { 1, 2 }, otherValues);
        observable.Unsubscribe(RemoveSelf);
        observable.Unsubscribe(otherValues.Add);
    }
}
