using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.Collection;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

/// <summary>
/// Verifies collection editor structure changes, identity reuse, and disposal.
/// </summary>
[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "PropertyEditors")]
public sealed class CollectionPropertyViewModelTests
{
    /// <summary>
    /// Adding and removing collection items updates count, names, parent links, and serialized list.
    /// </summary>
    [AvaloniaFact]
    public void AddAndRemoveUpdateCollectionStructure()
    {
        var property = TestCollection(10, 20);
        using var viewModel = TestCreateViewModel(property);

        viewModel.AddItemCommand.Execute(null);

        var items = Assert.IsType<List<SerializedProperty>>(property.Value);
        Assert.Equal(3, viewModel.Count);
        Assert.Equal("[2]", items[2].Name);
        Assert.Same(property, items[2].ParentProperty);
        Assert.Equal(0, items[2].Value);

        viewModel.Value[0].RemoveCommand.Execute(null);

        Assert.Equal(2, viewModel.Count);
        Assert.Equal(new[] { "[0]", "[1]" }, items.Select(x => x.Name).ToArray());
        Assert.Equal(new object?[] { 20, 0 }, items.Select(x => x.Value).ToArray());
    }

    /// <summary>
    /// Structure notification with unchanged item references preserves wrapper and child editor instances.
    /// </summary>
    [AvaloniaFact]
    public void UnchangedItemsKeepExistingEditors()
    {
        var property = TestCollection(4);
        using var viewModel = TestCreateViewModel(property);
        var wrapper = Assert.Single(viewModel.Value);
        var editor = Assert.Single(wrapper.Value);

        property.TriggerChangedEvent();

        Assert.Same(wrapper, Assert.Single(viewModel.Value));
        Assert.Same(editor, Assert.Single(wrapper.Value));
    }

    /// <summary>
    /// Replacing collection items disposes old child editors before rebuilding wrappers.
    /// </summary>
    [AvaloniaFact]
    public void ReplacementDisposesOldChildEditor()
    {
        var property = TestCollection(4);
        using var viewModel = TestCreateViewModel(property);
        var originalProperty = Assert.IsType<List<SerializedProperty>>(property.Value)[0];
        var originalEditor = Assert.IsType<IntegerPropertyViewModel>(Assert.Single(viewModel.Value[0].Value));
        property.Value = new List<SerializedProperty>
        {
            new("[0]", SerializedTypeEnum.Integer, 8, "int", property)
        };

        originalProperty.Value = 12;

        Assert.Equal(4, originalEditor.Value);
        Assert.Equal(8, Assert.IsType<IntegerPropertyViewModel>(Assert.Single(viewModel.Value[0].Value)).Value);
    }

    /// <summary>
    /// Collection editor rejects scalar properties at construction.
    /// </summary>
    [AvaloniaFact]
    public void ConstructorRejectsNonCollectionProperty()
    {
        var property = new SerializedProperty("value", SerializedTypeEnum.Integer, 1, "int", null);

        Assert.Throws<Exception>(() => TestCreateViewModel(property));
    }

    [AvaloniaFact]
    public async Task BackgroundReplacementUpdatesCollectionOnUiThread()
    {
        var property = TestCollection(4);
        using var vm = TestCreateViewModel(property);
        var notificationThreads = new List<bool>();
        vm.Value.CollectionChanged += (_, _) => notificationThreads.Add(Dispatcher.UIThread.CheckAccess());
        await Task.Run(() => property.Value = new List<SerializedProperty> { new("[0]", SerializedTypeEnum.Integer, 8, "int", property) });
        Dispatcher.UIThread.RunJobs();
        Assert.NotEmpty(notificationThreads);
        Assert.All(notificationThreads, isUiThread => Assert.True(isUiThread));
        Assert.Equal(8, Assert.IsType<IntegerPropertyViewModel>(Assert.Single(Assert.Single(vm.Value).Value)).Value);
    }

    [AvaloniaFact]
    public void QueuedCollectionUpdateIsIgnoredAfterDispose()
    {
        var property = TestCollection(4);
        var vm = TestCreateViewModel(property);
        using (Dispatcher.UIThread.DisableProcessing())
        {
            var worker = new Thread(() => property.Value = new List<SerializedProperty> { new("[0]", SerializedTypeEnum.Integer, 8, "int", property) });
            worker.Start();
            Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
            vm.Dispose();
        }
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(vm.Value);
    }

    private static CollectionPropertyViewModel TestCreateViewModel(SerializedProperty property)
        => new(property, null!, null!, null!, null!, null!, null!, null!, null!, null);

    private static SerializedProperty TestCollection(params int[] values)
    {
        var property = new SerializedProperty(
            "items",
            SerializedTypeEnum.Collection,
            null,
            "vector<int>",
            null,
            "int",
            SerializedTypeEnum.Integer,
            "int");
        property.Value = values.Select((value, index) =>
            new SerializedProperty($"[{index}]", SerializedTypeEnum.Integer, value, "int", property)).ToList();
        return property;
    }
}
