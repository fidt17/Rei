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

    public void LoseFocus()
    {
        SetValue(TextInternalProperty, GetValue(TextProperty));
        this.GetWindow().Focus();
    }

    public void Apply()
    {
        SetValue(TextProperty, GetValue(TextInternalProperty));
        this.GetWindow().Focus();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            SetValue(TextInternalProperty, GetValue(TextProperty));
        }
    }

    private void InputElement_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        SetValue(TextProperty, GetValue(TextInternalProperty));
    }
}