using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Editor.Views.Controls;

public partial class ContextMenuView : UserControl
{
	public ContextMenuView()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}