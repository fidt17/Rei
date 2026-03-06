using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ReiEditor.Views.Controls.Buttons;

public partial class HorizontalExpandToggle : UserControl
{
	public static readonly StyledProperty<bool> IsCheckedProperty = AvaloniaProperty.Register<HorizontalExpandToggle, bool>(
		"IsChecked");

	public bool IsChecked
	{
		get => GetValue(IsCheckedProperty);
		set => SetValue(IsCheckedProperty, value);
	}
	
	public HorizontalExpandToggle()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}