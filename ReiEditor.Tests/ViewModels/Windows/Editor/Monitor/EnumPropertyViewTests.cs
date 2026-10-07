using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using ReiEditor.Models.Services.Assets.Scripting.Serialization;
using ReiEditor.Models.Services.Assets.Scripting.Serialization.Types;
using ReiEditor.Models.Services.Components;
using ReiEditor.Tests.Infrastructure.Headless;
using ReiEditor.Tests.Infrastructure.TestDoubles;
using ReiEditor.ViewModels.Windows.Editor.Monitor.Drawers.Property;
using ReiEditor.Views.Windows.Editor.Monitor.Drawers.Property;

namespace ReiEditor.Tests.ViewModels.Windows.Editor.Monitor;

[Collection(HeadlessCollection.NAME)]
[Trait("Category", "Headless")]
[Trait("Area", "Monitor")]
public sealed class EnumPropertyViewTests
{
    [AvaloniaFact]
    public void ToneMappingLabelUsesSerializedPropertyName()
    {
        var registry = new SerializableObjectsRegistry(new TestLogger<SerializableObjectsRegistry>());
        registry.Replace([], [new SerializableEnum { EnumName = "ToneMappingMode", Options = new() { ["Off"] = 0, ["Reinhard"] = 1 } }]);
        var property = new SerializedProperty("_toneMapping", SerializedTypeEnum.Enum, 1, "ToneMappingMode", null);
        using var model = new EnumPropertyViewModel(property, registry);
        var styles = new StyleInclude(new Uri("avares://ReiEditor/")) { Source = new Uri("avares://ReiEditor/Views/Resources/Styles.axaml") };
        Application.Current!.Styles.Add(styles);
        var view = new EnumPropertyView { DataContext = model };
        var window = new Window { Content = view };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(model.PropertyName.Value, view.FindControl<TextBlock>("PropertyNameTextBlock")!.Text);
            Assert.Contains("Tone", model.PropertyName.Value);
            Assert.Contains("Mapping", model.PropertyName.Value);
        }
        finally
        {
            window.Close();
            Application.Current!.Styles.Remove(styles);
        }
    }
}
