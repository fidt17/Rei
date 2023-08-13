using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ReiEditor.Views.Windows;

public partial class EmptyView : UserControl
{
	public EmptyView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}