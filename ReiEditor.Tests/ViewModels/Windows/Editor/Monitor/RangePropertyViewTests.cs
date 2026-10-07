using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;
using ReiEditor.Views.Windows.Editor.Monitor.Drawers.Property;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Monitor")]
public sealed class RangePropertyViewTests
{
    private sealed class Ui : IDisposable
    {
        private readonly FluentTheme _theme = new();
        private readonly StyleInclude _styles = new(new Uri("avares://ReiEditor/")) { Source = new Uri("avares://ReiEditor/Views/Resources/Styles.axaml") };
        public RangePropertyViewModel Model { get; }
        public Window Window { get; }
        public Slider Slider { get; }
        public TextBox Input { get; }

        public Ui(SerializedProperty property, SerializedNumericRange range)
        {
            Application.Current!.Styles.Add(_theme);
            Application.Current.Styles.Add(_styles);
            Model = new(property, range);
            var view = new RangePropertyView { DataContext = Model };
            Slider = view.FindControl<Slider>("RangeSlider")!;
            Input = view.FindControl<TextBox>("RangeInput")!;
            Window = new Window { Width = 400, Height = 140, Content = view };
            Window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        public void Key(InputElement control, Key key)
        {
            control.Focus();
            control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose()
        {
            Window.Close();
            Model.Dispose();
            Application.Current!.Styles.Remove(_styles);
            Application.Current.Styles.Remove(_theme);
        }
    }

    [AvaloniaFact]
    public void InitialBoundsAndExternalUpdatesNeverWriteCoercedSliderValues()
    {
        var property = Integer(12);
        var changes = 0;
        property.ValueChangedEvent += _ => changes++;
        using var ui = new Ui(property, new(0, 8, null, true));
        Assert.Equal(0, ui.Slider.Minimum);
        Assert.Equal(8, ui.Slider.Maximum);
        Assert.Equal(8, ui.Slider.Value);
        Assert.Equal("12", ui.Input.Text);
        Assert.Equal(0, changes);
        ui.Input.Focus();
        ui.Slider.Focus();
        Assert.Equal(12, property.Value);
        Assert.Equal(0, changes);
        property.Value = -3;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, ui.Slider.Value);
        Assert.Equal("-3", ui.Input.Text);
        Assert.Equal(1, changes);
        Assert.True(ui.Slider.Bounds.Width > 80);
        Assert.True(ui.Input.Bounds.Width >= 70);
    }

    [AvaloniaFact]
    public void SliderKeysDragAndExactInputReachSerializedProperty()
    {
        var property = Integer(4);
        using var ui = new Ui(property, new(0, 8, null, true));
        ui.Key(ui.Slider, Key.Right);
        Assert.Equal(5, property.Value);
        ui.Key(ui.Slider, Key.Home);
        Assert.Equal(0, property.Value);
        ui.Key(ui.Slider, Key.End);
        Assert.Equal(8, property.Value);
        ui.Input.Text = "3";
        ui.Key(ui.Input, Key.Enter);
        Assert.Equal(3, property.Value);
        ui.Input.Text = "7";
        ui.Key(ui.Input, Key.Escape);
        Assert.Equal(3, property.Value);
        Assert.Equal("3", ui.Input.Text);
        ui.Input.Text = "2.5";
        ui.Key(ui.Input, Key.Enter);
        Assert.Equal(3, property.Value);
        Assert.True(ui.Model.HasStatus);
        ui.Key(ui.Input, Key.Escape);

        var thumb = ui.Slider.GetVisualDescendants().OfType<Thumb>().Single();
        var start = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), ui.Window)!.Value;
        var end = start + new Vector(30, 0);
        ui.Window.MouseMove(start);
        Dispatcher.UIThread.RunJobs();
        ui.Window.MouseDown(start, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        ui.Window.MouseMove(end);
        Dispatcher.UIThread.RunJobs();
        ui.Window.MouseUp(end, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.InRange(Assert.IsType<int>(property.Value), 4, 8);
        Assert.Equal(Convert.ToDouble(property.Value), ui.Slider.Value);
    }

    [AvaloniaFact]
    public void FloatSliderSnapsWhileTextKeepsPrecisionAndLostFocusCommits()
    {
        var property = new SerializedProperty("Weight", SerializedTypeEnum.Float, 0.5f, "f32", null);
        using var ui = new Ui(property, new(0, 1, 0.25, false));
        ui.Slider.Value = 0.26;
        Assert.Equal(0.25f, property.Value);
        Assert.Equal(0.25, ui.Slider.Value);
        ui.Input.Focus();
        ui.Input.Text = "0.125";
        ui.Slider.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0.125f, property.Value);
        Assert.Equal(0.125, ui.Slider.Value);
        ui.Input.Text = "NaN";
        ui.Key(ui.Input, Key.Enter);
        Assert.Equal(0.125f, property.Value);
    }

    [AvaloniaFact]
    public void FloatKeyboardStepMovesAcrossNarrowRangeAtLargeMagnitude()
    {
        const float MINIMUM = 10000000000f;
        var maximum = float.BitIncrement(MINIMUM);
        var property = new SerializedProperty("Weight", SerializedTypeEnum.Float, MINIMUM, "f32", null);
        using var ui = new Ui(property, new(MINIMUM, maximum, null, false));
        ui.Key(ui.Slider, Key.Right);
        Assert.Equal(maximum, property.Value);
        ui.Key(ui.Slider, Key.Left);
        Assert.Equal(MINIMUM, property.Value);
    }

    private static SerializedProperty Integer(object value) => new("Count", SerializedTypeEnum.Integer, value, "i32", null);
}