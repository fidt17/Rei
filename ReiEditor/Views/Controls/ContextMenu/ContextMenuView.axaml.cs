using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ReiEditor.Views.Controls.ContextMenu;

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