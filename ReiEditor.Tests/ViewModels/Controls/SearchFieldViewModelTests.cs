using ReiEditor.ViewModels.Controls;

namespace ReiEditor.Tests.ViewModels.Controls;

/// <summary>Verifies query state, placeholder focus, reset suppression and subscription lifetime.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "Search")]
public sealed class SearchFieldViewModelTests
{
    /// <summary>Whitespace is empty search; focus and nonempty text independently hide the placeholder.</summary>
    [Theory]
    [InlineData("", false, false, true)]
    [InlineData("  ", false, false, true)]
    [InlineData("", true, false, false)]
    [InlineData("Текст", false, true, false)]
    [InlineData("query", true, true, false)]
    public void QueryAndFocusDetermineDisplay(string query, bool focused, bool hasQuery, bool placeholder)
    {
        using var vm = new SearchFieldViewModel();
        vm.SetFocused(focused);
        vm.Query.Value = query;
        Assert.Equal(hasQuery, vm.HasQuery.Value);
        Assert.Equal(placeholder, vm.ShowPlaceholder.Value);
        vm.SetFocused(false);
        Assert.Equal(!hasQuery, vm.ShowPlaceholder.Value);
    }

    /// <summary>Reset suppresses refresh only during notification; explicit clear does not suppress it.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClearingPublishesWithExpectedSuppression(bool reset)
    {
        using var vm = new SearchFieldViewModel();
        vm.Query.Value = "before";
        var notifications = new List<(string Query, bool Suppressed)>();
        vm.Query.ChangedEvent += query => notifications.Add((query, vm.ShouldSuppressQueryRefresh()));
        if (reset) vm.ResetSearch();
        else vm.ClearCommand.Execute(null);
        Assert.Equal(new[] { ("", reset) }, notifications);
        Assert.False(vm.ShouldSuppressQueryRefresh());
        Assert.False(vm.HasQuery.Value);
    }

    /// <summary>Disposal detaches derived state updates from the query field.</summary>
    [Fact]
    public void DisposeDetachesQueryHandler()
    {
        var vm = new SearchFieldViewModel();
        vm.Dispose();
        vm.Query.Value = "after disposal";
        Assert.False(vm.HasQuery.Value);
        Assert.True(vm.ShowPlaceholder.Value);
    }
}
