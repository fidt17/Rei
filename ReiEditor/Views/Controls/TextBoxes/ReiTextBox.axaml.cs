using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ReiEditor.Views.Utils;

namespace ReiEditor.Views.Controls.TextBoxes;

public partial class ReiTextBox : UserControl
{
    public static readonly StyledProperty<string> TextInternalProperty = AvaloniaProperty.Register<ReiTextBox, string>("TextInternal");
    public string TextInternal
    {
        get => GetValue(TextInternalProperty);
        set => SetValue(TextInternalProperty, value);
    }

    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<ReiTextBox, string>("Text");
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ReiTextBox()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void LoseFocus() => ApplyValue();

    public void Apply() => this.GetWindow().Focus();

    private void ApplyValue()
    {
        var oldValue = GetValue(TextProperty);
        var targetValue = GetValue(TextInternalProperty);
        SetValue(TextProperty, GetValue(TextInternalProperty));
        var actualValue = GetValue(TextProperty);

        if (targetValue != actualValue)
        {
            SetValue(TextProperty, oldValue);
            SetValue(TextInternalProperty, oldValue);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            SetValue(TextInternalProperty, GetValue(TextProperty));
        }
    }

    // ReSharper disable once UnusedParameter.Local
    private void InputElement_OnLostFocus(object? sender, RoutedEventArgs _) => ApplyValue();
}