using ReiEditor.Utils.Common;

namespace ReiEditor.Tests.Utils.Common;

/// <summary>
/// Verifies observable field notifications, null access, and owned value disposal.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Area", "Common")]
public sealed class ObservableFieldTests
{
    private sealed class TestDisposable : IDisposable
    {
        public int DisposeCalls { get; private set; }

        public void Dispose() => DisposeCalls++;
    }

    /// <summary>
    /// Changed assignments publish both notification channels after updating the field.
    /// </summary>
    [Fact]
    public void ChangedValuePublishesPropertyAndValueNotifications()
    {
        var field = new ObservableField<int>(1);
        var properties = new List<string?>();
        var values = new List<int>();
        field.PropertyChanged += (_, args) =>
        {
            Assert.Equal(2, field.Get());
            properties.Add(args.PropertyName);
        };
        field.ChangedEvent += value =>
        {
            Assert.Equal(value, field.Value);
            values.Add(value);
        };

        field.Set(2);

        Assert.Equal(new[] { nameof(field.Value) }, properties);
        Assert.Equal(new[] { 2 }, values);
    }

    /// <summary>
    /// Equal values suppress both events even when supplied through a distinct reference.
    /// </summary>
    [Fact]
    public void EqualValueDoesNotNotify()
    {
        var field = new ObservableField<string>("same");
        var notificationCount = 0;
        field.PropertyChanged += (_, _) => notificationCount++;
        field.ChangedEvent += _ => notificationCount++;

        field.Set(new string("same".ToCharArray()));

        Assert.Equal("same", field.Get());
        Assert.Equal(0, notificationCount);
    }

    /// <summary>
    /// A field initialized with null rejects reads until assigned a non-null value.
    /// </summary>
    [Fact]
    public void NullInitialValueRejectsReadsAndCanBeReplaced()
    {
        var field = new ObservableField<string?>(null);

        Assert.Throws<NullReferenceException>(() => field.Value);
        Assert.Throws<NullReferenceException>(() => field.Get());
        field.Set("ready");

        Assert.Equal("ready", field.Get());
    }

    /// <summary>
    /// Disposal forwards to the current disposable value and accepts ordinary values.
    /// </summary>
    [Fact]
    public void DisposeReleasesCurrentDisposableValue()
    {
        var value = new TestDisposable();
        var field = new ObservableField<TestDisposable>(value);

        field.Dispose();
        new ObservableField<int>(42).Dispose();

        Assert.Equal(1, value.DisposeCalls);
    }
}
