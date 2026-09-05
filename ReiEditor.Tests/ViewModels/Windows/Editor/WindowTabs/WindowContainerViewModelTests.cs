using ReiEditor.Models.Services.Preferences;
using ReiEditor.Models.Services.Serialization;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Common;
using ReiEditor.ViewModels.Windows.Editor.WindowTabs;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.WindowTabs;

/// <summary>Verifies preferred tab restoration, exclusive selection and preference writes.</summary>
[Trait("Category", "Unit")]
[Trait("Area", "WindowTabs")]
public sealed class WindowContainerViewModelTests
{
    /// <summary>Adding a preferred tab selects it; commands switch one active tab and repeated selection does not save again.</summary>
    [Theory]
    [InlineData("Monitor", "Monitor")]
    [InlineData("missing", "Hierarchy")]
    [InlineData("monitor", "Hierarchy")]
    public async Task PreferredTabAndCommandSelectionPersistOnce(string preferred, string expected)
    {
        var writes = new List<string>();
        var preferences = new EditorPreferencesService(new TestEditorStorageService(_ => Task.FromResult<string?>("{}"), (_, value) => { writes.Add(value); return Task.FromResult(true); }), new TestLogger<EditorPreferencesService>(), new JsonSerializer());
        await preferences.InitializeAsync();
        preferences.SetWindowContainerActiveTab("left", preferred);
        using var vm = new WindowContainerViewModel(preferences, "left");
        using var hierarchy = new EmptyViewModel();
        using var monitor = new EmptyViewModel();
        using var header = new EmptyViewModel();
        vm.AddTab("Hierarchy", hierarchy);
        vm.AddTab("Monitor", monitor, header);
        Assert.Equal(expected, vm.ActiveTab!.Name);
        Assert.Single(vm.Tabs, tab => tab.IsActive);
        Assert.Same(header, vm.Tabs[1].HeaderContent);
        var target = vm.Tabs.Single(tab => !tab.IsActive);
        var before = writes.Count;
        target.SelectCommand.Execute(null);
        target.SelectCommand.Execute(null);
        Assert.Same(target, vm.ActiveTab);
        Assert.Same(target, Assert.Single(vm.Tabs, tab => tab.IsActive));
        Assert.Equal(target.Name, preferences.GetWindowContainerActiveTab("left"));
        Assert.Equal(before + 1, writes.Count);
    }

    /// <summary>A container without preferences still selects the first tab and supports switching content.</summary>
    [Fact]
    public void StandaloneContainerSelectsAndSwitchesTabs()
    {
        using var vm = new WindowContainerViewModel();
        using var first = new EmptyViewModel();
        using var second = new EmptyViewModel();
        Assert.Null(vm.ActiveTab);
        vm.AddTab("First", first);
        vm.AddTab("Second", second);
        Assert.Same(first, vm.ActiveTab!.Content);
        vm.Tabs[1].SelectCommand.Execute(null);
        Assert.Same(second, vm.ActiveTab.Content);
        Assert.False(vm.Tabs[0].IsActive);
        Assert.True(vm.Tabs[1].IsActive);
    }
}
