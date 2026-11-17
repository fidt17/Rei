using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReiEditor.Models.Services.Assets.Scripting;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Views.Controls.TextBoxes;
using ReiEditor.Views.Windows.Editor.Monitor.Drawers.Property.Custom;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

[Collection(HeadlessCollection.NAME)]
public sealed partial class RectTransformPropertyViewModelTests
{
    [Theory]
    [InlineData("HorizontalPrimaryValue")]
    [InlineData("HorizontalSecondaryValue")]
    [InlineData("VerticalPrimaryValue")]
    [InlineData("VerticalSecondaryValue")]
    [InlineData("AnchorMinX")]
    [InlineData("AnchorMinY")]
    [InlineData("AnchorMaxX")]
    [InlineData("AnchorMaxY")]
    [InlineData("PivotX")]
    [InlineData("PivotY")]
    public void LayoutNotificationCannotWritePreviousBoundValueBackToEngine(string propertyName)
    {
        var fixture = TestCreateFixture();
        using var vm = fixture.ViewModel;
        vm.AnchorMinX = vm.AnchorMinY = 0.25f;
        vm.AnchorMaxX = vm.AnchorMaxY = 0.75f;
        fixture.Runner.Active.Value = fixture.Runner.EditorActive.Value = true;
        var property = vm.GetType().GetProperty(propertyName)!;
        var previous = property.GetValue(vm);
        var echoed = false;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != propertyName || echoed) return;
            echoed = true;
            // Captured in the real-engine trace: a TwoWay setter runs inside the native-refresh notification.
            property.SetValue(vm, previous);
        };
        fixture.Component.GetProperty(EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION)
            .SetValueWithoutTriggeringChangedEvent(new Dictionary<string, object?> { ["x"] = 0f, ["y"] = 0f });
        Assert.True(echoed);
        Assert.Empty(fixture.Api.Requests);
        Assert.Equal(0f, Convert.ToSingle(TestVector(fixture.Component, EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION)["x"].Value));
        Assert.Equal(0f, Convert.ToSingle(TestVector(fixture.Component, EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION)["y"].Value));
    }

    [AvaloniaFact]
    public void ExternalStretchLayoutRefreshThroughBindingsDoesNotWriteToEngine()
    {
        var fixture = TestCreateFixture();
        using var vm = fixture.ViewModel;
        vm.AnchorMinX = vm.AnchorMinY = 0.25f;
        vm.AnchorMaxX = vm.AnchorMaxY = 0.75f;
        fixture.Runner.Active.Value = true;
        fixture.Runner.EditorActive.Value = true;
        var theme = new FluentTheme();
        var styles = new StyleInclude(new Uri("avares://ReiEditor/")) { Source = new Uri("avares://ReiEditor/Views/Resources/Styles.axaml") };
        Application.Current!.Styles.Add(theme);
        Application.Current.Styles.Add(styles);
        var view = new RectTransformPropertyView { DataContext = vm };
        var window = new Window { Content = view, Width = 600, Height = 600 };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(fixture.Api.Requests);
            var inputs = view.GetVisualDescendants().OfType<ReiTextBox>().Take(4).Select(input => (TextBox)input.Content!).ToArray();
            Assert.Equal(4, inputs.Length);
            for (var i = 0; i < 20; i++)
            {
                Assert.True(inputs[i % 4].Focus());
                var resized = i % 2 == 0;
                fixture.Component.GetProperty(EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION)
                    .SetValueWithoutTriggeringChangedEvent(new Dictionary<string, object?> { ["x"] = resized ? 12f : 0f, ["y"] = resized ? -10f : 0f });
                fixture.Component.GetProperty(EngineBehavioursConstants.RECT_TRANSFORM_SIZE_DELTA)
                    .SetValueWithoutTriggeringChangedEvent(new Dictionary<string, object?> { ["x"] = resized ? -26f : -50f, ["y"] = resized ? -20f : -40f });
                Dispatcher.UIThread.RunJobs();
                Assert.True(inputs[(i + 1) % 4].Focus());
                Assert.Empty(fixture.Api.Requests);
                Assert.Equal(resized ? 12f : 0f, Convert.ToSingle(TestVector(fixture.Component, EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION)["x"].Value));
                Assert.Equal(resized ? -10f : 0f, Convert.ToSingle(TestVector(fixture.Component, EngineBehavioursConstants.RECT_TRANSFORM_ANCHORED_POSITION)["y"].Value));
            }
        }
        finally
        {
            window.Close();
            Application.Current.Styles.Remove(theme);
            Application.Current.Styles.Remove(styles);
        }
    }
}
