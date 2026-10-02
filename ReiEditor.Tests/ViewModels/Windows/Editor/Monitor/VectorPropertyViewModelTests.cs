using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Newtonsoft.Json.Linq;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property.Custom.Vector;
using ReiEditor.Views.Controls.TextBoxes;
using ReiEditor.Views.Windows.Editor.Monitor.Drawers.Property.Custom;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Monitor")]
public sealed class VectorPropertyViewModelTests
{
    [AvaloniaFact]
    public Task Vector3ViewUpdatesFromSnapshotsWithoutReselection() => VerifyViewAsync(3);

    [AvaloniaFact]
    public Task Vector2ViewUpdatesFromSnapshotsWithoutReselection() => VerifyViewAsync(2);

    private static async Task VerifyViewAsync(int dimensions)
    {
        var children = new Dictionary<string, SerializedProperty>();
        foreach (var axis in new[] { "x", "y", "z" }.Take(dimensions))
            children[axis] = new(axis, SerializedTypeEnum.Float, 1f, "float", null);
        var property = new SerializedProperty("Position", SerializedTypeEnum.Custom, children, $"Vector{dimensions}", null);
        using BaseCustomPropertyViewModel vm = dimensions == 3 ? new Vector3PropertyViewModel(property) : new Vector2PropertyViewModel(property);
        var resources = Application.Current!.Resources;
        var hadBrush = resources.ContainsKey("Gray_4");
        var oldBrush = hadBrush ? resources["Gray_4"] : null;
        resources["Gray_4"] = Brushes.Gray;
        var theme = new FluentTheme();
        Application.Current.Styles.Add(theme);
        var window = new Window();
        try
        {
            UserControl view = dimensions == 3 ? new Vector3PropertyView() : new Vector2PropertyView();
            view.DataContext = vm;
            window.Content = view;
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var fields = view.GetVisualDescendants().OfType<ReiTextBox>().ToArray();
            Assert.Equal(dimensions, fields.Length);
            Assert.All(fields, field => Assert.Equal("1", field.TextInternal));
            var registry = new SerializableObjectsRegistry(new TestLogger<SerializableObjectsRegistry>());
            var properties = new SerializedPropertiesService(registry, new TestLogger<SerializedPropertiesService>());
            var snapshot = new JObject { ["x"] = 12f, ["y"] = -34f };
            if (dimensions == 3) snapshot["z"] = 56f;
            properties.ApplyValue(property, snapshot);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(12f, vm is Vector3PropertyViewModel vector3 ? vector3.X : ((Vector2PropertyViewModel)vm).X);
            Assert.Equal(new[] { "12", "-34", "56" }.Take(dimensions), fields.Select(field => field.TextInternal));
            Assert.Equal(new[] { "12", "-34", "56" }.Take(dimensions), fields.Select(field => field.GetVisualDescendants().OfType<TextBox>().Single().Text));

            // A direct child update must refresh the same controls too.
            children["x"].Value = 78f;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("78", fields[0].TextInternal);

            await Task.Run(() => properties.ApplyValue(property, new JObject { ["x"] = 80f }));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("80", fields[0].TextInternal);
            var notifications = new List<string?>();
            vm.PropertyChanged += (_, change) => notifications.Add(change.PropertyName);
            properties.ApplyValue(property, new JObject { ["x"] = 80f });
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(notifications);

            // Editing the existing field still writes the nested serialized value.
            fields[0].TextInternal = "90";
            fields[0].LoseFocus();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(90f, Convert.ToSingle(children["x"].Value));
            Assert.Equal("90", fields[0].TextInternal);
            vm.Dispose();
            children["x"].Value = 99f;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("90", fields[0].TextInternal);
        }
        finally
        {
            window.Close();
            Application.Current.Styles.Remove(theme);
            if (hadBrush) resources["Gray_4"] = oldBrush;
            else resources.Remove("Gray_4");
        }
    }
}
