using ReiEditor.Models.Services.Components;
using ReiEditor.Tests.Infrastructure.Builders;

namespace ReiEditor.Tests.Models.Services.Components;

/// <summary>
/// Verifies component property ownership and transform hierarchy notification contracts.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Components")]
public sealed class ComponentTests
{
    /// <summary>
    /// Adding and removing properties preserves identity, rejects duplicates without mutation, and tolerates missing removal.
    /// </summary>
    [Fact]
    public void BehaviourPropertyLifecyclePreservesIdentityAndRejectsDuplicates()
    {
        var component = new BehaviourComponent(17);
        var original = SerializedPropertyBuilder.Integer("count", 3);
        component.AddProperty(original);

        Assert.True(component.HasProperty("count"));
        Assert.Same(original, component.GetProperty("count"));
        Assert.Throws<Exception>(() => component.AddProperty(SerializedPropertyBuilder.Integer("count", 4)));
        Assert.Same(original, Assert.Single(component.Properties).Value);

        component.RemoveProperty("missing");
        Assert.Single(component.Properties);
        component.RemoveProperty("count");
        component.RemoveProperty("count");

        Assert.False(component.HasProperty("count"));
        Assert.Empty(component.Properties);
        Assert.Throws<KeyNotFoundException>(() => component.GetProperty("count"));
    }

    /// <summary>
    /// Transform order changes publish the committed order once while repeated values stay quiet.
    /// </summary>
    [Fact]
    public void TransformOrderPublishesOnlyChangesAfterStateUpdate()
    {
        var transform = new TransformComponent();
        var orders = new List<int>();
        transform.OrderChangedEvent += order =>
        {
            Assert.Equal(order, transform.Order);
            orders.Add(order);
        };

        transform.SetOrder(0);
        transform.SetOrder(4);
        transform.SetOrder(4);
        transform.SetOrder(0);

        Assert.Equal(new[] { 4, 0 }, orders);
    }

    /// <summary>
    /// Parent zero means root; changing parent does not alter sibling order or emit order notifications.
    /// </summary>
    [Fact]
    public void TransformParentCanReturnToRootWithoutChangingOrder()
    {
        var transform = new TransformComponent();
        transform.SetOrder(3);
        var notifications = 0;
        transform.OrderChangedEvent += _ => notifications++;
        Assert.False(transform.HasParent());

        transform.SetParent(42);
        Assert.True(transform.HasParent());
        Assert.Equal(42, transform.Parent);
        transform.SetParent(0);

        Assert.False(transform.HasParent());
        Assert.Equal(0, transform.Parent);
        Assert.Equal(3, transform.Order);
        Assert.Equal(0, notifications);
    }
}
